# FastDFS.Client

A modern, high-performance FastDFS .NET client library with built-in connection pooling and multi-cluster support.

[![NuGet](https://img.shields.io/nuget/v/FastDFS.Client.svg)](https://www.nuget.org/packages/FastDFS.Client/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Features

- ✅ **High Performance**: Uses modern Socket API instead of TcpClient for better throughput and lower latency
- ✅ **Target Framework**: netstandard2.0 (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- ✅ **Connection Pooling**: Automatic connection management with configurable pool sizes
- ✅ **Shared Connection Pool**: Clients with identical configurations share the same connection pool, eliminating redundant TCP connections
- ✅ **Multi-Cluster Support**: Manage multiple FastDFS clusters with named clients
- ✅ **Dynamic Client Registration**: Register and remove clients at runtime (e.g., from a database)
- ✅ **Full Async/Await**: All operations are fully asynchronous
- ✅ **Dependency Injection**: First-class DI support with IOptions pattern
- ✅ **Non-DI Support**: Factory pattern for non-DI scenarios
- ✅ **Logging Integration**: Built-in logging with `Microsoft.Extensions.Logging`
- ✅ **Automatic Failover**: Tracker server failover support
- ✅ **Low-Level Access**: Direct access to `ITrackerClient` and `IStorageClient` for advanced use cases
- ✅ **Zero Dependencies**: Core library has no external dependencies
- ✅ **Comprehensive Tests**: 86+ unit tests with 100% pass rate

## Architecture

The SDK is structured in three clear layers:

```
IFastDFSClient (Coordination Layer)
  ├── ITrackerClient  — Queries tracker servers to locate storage nodes
  └── IStorageClient  — Executes protocol commands against storage servers
                         and manages per-node connection pools
```

- **`IFastDFSClient`** — Main API. Coordinates tracker queries and storage operations. This is what you inject and use day-to-day.
- **`ITrackerClient`** — Communicates with tracker servers to resolve storage node addresses. Handles automatic failover across multiple trackers.
- **`IStorageClient`** — Executes FastDFS binary protocol commands against a specific storage server. Manages a dynamic connection pool per storage node.

## Quick Start

### Installation

```bash
dotnet add package FastDFS.Client
dotnet add package FastDFS.Client.DependencyInjection  # for DI support
```

### Basic Usage (Dependency Injection)

**Single Cluster:**

```csharp
// Program.cs
services.AddFastDFS(options =>
{
    options.TrackerServers = new[] { "192.168.1.100:22122" };
    options.ConnectionPool = new ConnectionPoolConfiguration
    {
        MaxConnectionPerServer = 50,
        MinConnectionPerServer = 5
    };
});

// Usage in your service
public class FileService
{
    private readonly IFastDFSClient _client;

    public FileService(IFastDFSClient client)
    {
        _client = client;
    }

    public async Task<string> UploadFile(byte[] content, string extension)
    {
        // Upload returns file ID like: group1/M00/00/00/wKgBaGVlYWRlYS5qcGc
        return await _client.UploadAsync(null, content, extension);
    }
}
```

**Multiple Clusters:**

```csharp
// Register multiple clusters
services.AddFastDFS("default", options =>
{
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});

services.AddFastDFS("backup", options =>
{
    options.TrackerServers = new[] { "192.168.2.100:22122" };
});

// Usage
public class FileService
{
    private readonly IFastDFSClient _defaultClient;
    private readonly IFastDFSClient _backupClient;

    public FileService(IFastDFSClientFactory factory)
    {
        _defaultClient = factory.GetClient("default");
        _backupClient  = factory.GetClient("backup");
    }
}
```

**Factory API:**

```csharp
// Get default client
var defaultClient = factory.GetClient();

// Get named client
var backupClient = factory.GetClient("backup");

// Check if a client exists
if (factory.HasClient("cdn"))
{
    var cdnClient = factory.GetClient("cdn");
}

// Get all client names
var clientNames = factory.GetClientNames();

// Register client dynamically at runtime
factory.RegisterClient("new-cluster", new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.3.100:22122" }
});

// Remove a client
factory.RemoveClient("backup");

// Register without any pre-configured clients (pure dynamic registration)
// services.AddFastDFS(); — then register all clients at runtime via RegisterClient
```

### Configuration (appsettings.json)

```json
{
  "FastDFS": {
    "Clusters": {
      "default": {
        "TrackerServers": ["192.168.1.100:22122", "192.168.1.101:22122"],
        "ConnectionPool": {
          "MaxConnectionPerServer": 50,
          "MinConnectionPerServer": 5,
          "ConnectionIdleTimeout": 300,
          "ConnectionLifetime": 3600
        },
        "NetworkTimeout": 30,
        "Charset": "UTF-8"
      },
      "backup": {
        "TrackerServers": ["192.168.2.100:22122"],
        "ConnectionPool": {
          "MaxConnectionPerServer": 30,
          "MinConnectionPerServer": 3
        }
      }
    }
  }
}
```

```csharp
services.AddFastDFS(configuration.GetSection("FastDFS"));
```

### Non-DI Usage

`IFastDFSClient` implements `IDisposable`. Always dispose the client (or use `using`) to release TCP connections cleanly.

**Single client:**

```csharp
var options = new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.1.100:22122" }
};

using var client = FastDFSClientBuilder.CreateClient(options);

var fileId = await client.UploadAsync(null, fileBytes, "jpg");
var content = await client.DownloadAsync(fileId);
await client.DeleteAsync(fileId);
```

**Multiple Clusters (Non-DI):**

Use `FastDFSClientManager` for multi-cluster scenarios — it owns and disposes all clients.

```csharp
using var manager = new FastDFSClientManager();

manager.AddClient("default", new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.1.100:22122" }
});

manager.AddClient("backup", new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.2.100:22122" }
});

var defaultClient = manager.GetClient("default");
var backupClient  = manager.GetClient("backup");

// Register client dynamically
manager.RegisterClient("new-cluster", new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.3.100:22122" }
});

// Remove client
manager.RemoveClient("backup");

// Dispose manager → disposes all managed clients and TCP connections
```

## API Overview

### Upload Operations

```csharp
// Upload from byte array (auto-select group)
string fileId = await client.UploadAsync(null, bytes, "jpg");
// Returns: "group1/M00/00/00/wKgBaGVlYWRlYS5qcGc"

// Upload to specific group
string fileId = await client.UploadAsync("group1", bytes, "jpg");

// Upload from stream
string fileId = await client.UploadAsync(null, stream, "pdf");

// Upload from file path
string fileId = await client.UploadFileAsync(null, "/path/to/file.png");

// Upload appender file (supports append later)
string fileId = await client.UploadAppenderFileAsync(null, bytes, "log");

// Append to appender file
await client.AppendFileAsync(fileId, newBytes);
```

### Download Operations

```csharp
// Download to byte array
byte[] content = await client.DownloadAsync(fileId);

// Download to stream
await client.DownloadAsync(fileId, outputStream);

// Download to file
await client.DownloadFileAsync(fileId, "/save/path.jpg");

// Partial download (offset and length)
byte[] partial = await client.DownloadAsync(fileId, offset: 1024, length: 2048);
```

### File Management

```csharp
// Query file info
FastDFSFileInfo info = await client.QueryFileInfoAsync(fileId);
Console.WriteLine($"Size: {info.FileSize}, CRC32: {info.Crc32}");

// Check if file exists
bool exists = await client.FileExistsAsync(fileId);

// Delete file
await client.DeleteAsync(fileId);

// Set metadata
var metadata = new FastDFSMetadata
{
    { "author", "John" },
    { "created", "2024-01-01" },
    { "width", "1920" },
    { "height", "1080" }
};
await client.SetMetadataAsync(fileId, metadata, MetadataFlag.Overwrite);

// Get metadata
FastDFSMetadata metadata = await client.GetMetadataAsync(fileId);
string author = metadata["author"];
```

## Advanced Usage

### Low-Level Tracker and Storage Access

`IFastDFSClient` exposes `TrackerClient` and `StorageClient` for scenarios that require direct control over the underlying protocol.

```csharp
// Query the tracker directly — get storage server info for upload
StorageServerInfo server = await client.TrackerClient.QueryStorageForUploadAsync("group1");

// Execute storage operation directly against the resolved server
string fileId = await client.StorageClient.UploadAsync(server, bytes, "jpg");

// Query all available storage servers for a file (for custom selection logic)
var servers = await client.TrackerClient.QueryAllStoragesForDownloadAsync("group1", fileName);

// Execute a download against a specific server
byte[] content = await client.StorageClient.DownloadAsync(server, "group1", fileName, offset: 0, length: 0);
```

This is useful for:
- Custom storage server selection logic
- Batch operations targeting a single storage node
- Implementing custom retry or circuit-breaker policies at the protocol level

### Connection Pool Configuration

```csharp
services.AddFastDFS(options =>
{
    options.TrackerServers = new[] { "192.168.1.100:22122", "192.168.1.101:22122" };
    options.ConnectionPool = new ConnectionPoolConfiguration
    {
        MaxConnectionPerServer = 50,   // Maximum connections per server
        MinConnectionPerServer = 5,    // Minimum connections (pre-warmed)
        ConnectionIdleTimeout  = 300,  // Idle timeout in seconds
        ConnectionLifetime     = 3600, // Max lifetime in seconds
        ConnectionTimeout      = 30000, // Connection timeout in ms
        SendTimeout            = 30000, // Send timeout in ms
        ReceiveTimeout         = 30000  // Receive timeout in ms
    };
    options.Charset = "UTF-8";
    options.NetworkTimeout = 30;
});
```

### Storage Server Selection Strategy

```csharp
services.AddFastDFS(options =>
{
    options.TrackerServers = new[] { "192.168.1.100:22122" };

    // Let the tracker decide (default, most efficient)
    options.StorageSelectionStrategy = StorageSelectionStrategy.TrackerSelection;

    // Or choose client-side strategy:
    // options.StorageSelectionStrategy = StorageSelectionStrategy.RoundRobin;
    // options.StorageSelectionStrategy = StorageSelectionStrategy.Random;
    // options.StorageSelectionStrategy = StorageSelectionStrategy.FirstAvailable;
});
```

### HTTP URL Generation (for FastDFS Nginx Module)

FastDFS supports HTTP access through the **fastdfs-nginx-module**. This SDK can generate HTTP URLs for files:

```csharp
services.AddFastDFS(options =>
{
    options.TrackerServers = new[] { "192.168.1.100:22122" };

    options.HttpConfig = new HttpConfiguration
    {
        // Option 1: Configure HTTP server URLs per group
        ServerUrls = new Dictionary<string, string>
        {
            { "group1", "http://img1.example.com" },
            { "group2", "http://img2.example.com" }
        },

        // Option 2: Use template with storage server IP
        // DefaultServerUrlTemplate = "http://{ip}:8080",

        // Anti-steal token (optional, requires Nginx module setup)
        AntiStealTokenEnabled      = true,
        SecretKey                  = "your-secret-key-here",
        DefaultTokenExpireSeconds  = 3600
    };
});
```

**Generate HTTP URLs:**

```csharp
// Simple HTTP URL
string url = await client.GetFileUrlAsync(fileId);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg

// With custom download filename
string url = await client.GetFileUrlAsync(fileId, "my-photo.jpg");
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?attname=my-photo.jpg

// Secure URL with anti-steal token
string secureUrl = await client.GetFileUrlWithTokenAsync(fileId, expireSeconds: 3600);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?token=abc123&ts=1234567890

// Secure URL with custom filename
string secureUrl = await client.GetFileUrlWithTokenAsync(fileId, 3600, "photo.jpg");
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?token=abc123&ts=1234567890&attname=photo.jpg
```

### Logging Integration

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

services.AddFastDFS(options =>
{
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});

// Logs automatically include:
// - Connection pool events (creation, reuse, disposal)
// - Tracker failover events
// - Upload/download operations
// - Network errors
```

### Error Handling

```csharp
try
{
    var fileId = await client.UploadAsync(null, bytes, "jpg");
}
catch (FastDFSNetworkException ex)
{
    // Network-related errors (connection failed, timeout, etc.)
    _logger.LogError(ex, "Network error: {Endpoint}", ex.RemoteEndpoint);
}
catch (FastDFSProtocolException ex)
{
    // Protocol errors (invalid response, server error, etc.)
    _logger.LogError(ex, "Protocol error: {ErrorCode}", ex.ErrorCode);
}
catch (FastDFSException ex)
{
    // General FastDFS errors
    _logger.LogError(ex, "FastDFS error: {Message}", ex.Message);
}
```

### Multi-Cluster Failover Example

```csharp
public class RobustFileService
{
    private readonly IFastDFSClient _primaryClient;
    private readonly IFastDFSClient _backupClient;
    private readonly ILogger<RobustFileService> _logger;

    public RobustFileService(IFastDFSClientFactory factory, ILogger<RobustFileService> logger)
    {
        _primaryClient = factory.GetClient("primary");
        _backupClient  = factory.GetClient("backup");
        _logger        = logger;
    }

    public async Task<string> UploadWithFailover(byte[] content, string extension)
    {
        try
        {
            return await _primaryClient.UploadAsync(null, content, extension);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary cluster failed, switching to backup");
            return await _backupClient.UploadAsync(null, content, extension);
        }
    }
}
```

## Object Lifecycle

### DI Scenario

All clients are singletons managed by `FastDFSClientFactory`. The DI container disposes the factory (and all underlying TCP connections) when the application shuts down. No manual cleanup is needed.

```
DI Container (Singleton)
  └── FastDFSClientFactory  ← sole lifecycle owner
        └── FastDFSClient   ← one per named cluster
              ├── TrackerClient → ConnectionPool(s) → TCP connections
              └── StorageClient → ConnectionPool(s) → TCP connections (per storage node)
```

### Non-DI Scenario

| Entry Point | Lifecycle management |
|---|---|
| `FastDFSClientBuilder.CreateClient()` | Caller owns the client — use `using` or call `Dispose()` explicitly |
| `FastDFSClientManager` | Manager owns all clients — dispose the manager to release everything |

```csharp
// FastDFSClientBuilder — caller owns
using var client = FastDFSClientBuilder.CreateClient(options);

// FastDFSClientManager — manager owns
using var manager = new FastDFSClientManager();
manager.AddClient("default", options);
var client = manager.GetClient("default"); // do NOT dispose individually
```

## Performance Tips

1. **Reuse Clients**: `IFastDFSClient` instances are thread-safe and designed for long-term reuse
2. **Tune Pool Size**: Adjust `MaxConnectionPerServer` to match your workload concurrency
3. **Shared Connection Pool**: Multiple named clients with identical configuration automatically share one connection pool — no extra setup needed
4. **Use Async**: All operations are async — always `await` to avoid thread starvation
5. **Storage Selector**: `TrackerSelection` (default) is the most efficient strategy; use client-side strategies only when you need custom routing

## Building

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Pack NuGet package
dotnet pack -c Release
```

## Project Structure

```
FastDFS.Client/
├── src/
│   ├── FastDFS.Client/                     # Core library (zero dependencies)
│   │   ├── Protocol/                       # FastDFS binary protocol (requests/responses)
│   │   ├── Connection/                     # TCP connection and pooling
│   │   ├── Tracker/                        # ITrackerClient — tracker server communication
│   │   ├── Storage/                        # IStorageClient — storage server operations
│   │   ├── Configuration/                  # Configuration models
│   │   ├── Exceptions/                     # Custom exception types
│   │   └── Utilities/                      # Helper utilities
│   └── FastDFS.Client.DependencyInjection/ # DI extensions (AddFastDFS, IFastDFSClientFactory)
├── tests/
│   └── FastDFS.Client.Tests/               # Unit tests (86+ tests)
├── benchmarks/
│   └── FastDFS.Client.Benchmarks/          # BenchmarkDotNet performance tests
└── samples/
    └── FastDFS.Client.Samples/             # Usage examples
```

## Requirements

- FastDFS Server 6.0+ (recommended)
- .NET Standard 2.0 compatible runtime
  - .NET Framework 4.6.1+
  - .NET Core 2.0+
  - .NET 5.0+
  - .NET 6.0+
  - .NET 7.0+
  - .NET 8.0+

## License

MIT License - see [LICENSE](LICENSE) file for details

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Support

- 📖 [Documentation](CLAUDE.md)
- 🐛 [Issue Tracker](https://github.com/yourusername/FastDFS-dotnet/issues)
- 💬 [Discussions](https://github.com/yourusername/FastDFS-dotnet/discussions)
