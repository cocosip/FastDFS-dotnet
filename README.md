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
- ✅ **Zero Dependencies**: Core library has no external dependencies
- ✅ **Comprehensive Tests**: 86+ unit tests with 100% pass rate

## Quick Start

### Installation

```bash
dotnet add package FastDFS.Client
```

### Basic Usage (Dependency Injection)

**Single Cluster:**

```csharp
// Startup.cs or Program.cs
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
        return await _client.UploadAsync(null, content, extension, CancellationToken.None);
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
        _backupClient = factory.GetClient("backup");
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

```csharp
var options = new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.1.100:22122" }
};

var client = FastDFSClientBuilder.CreateClient(options);

// Upload file (groupName can be null for auto-select)
var fileId = await client.UploadAsync(null, fileBytes, "jpg", CancellationToken.None);

// Download file
var content = await client.DownloadAsync(fileId, CancellationToken.None);

// Delete file
await client.DeleteAsync(fileId, CancellationToken.None);
```

**Multiple Clusters (Non-DI):**

```csharp
var manager = new FastDFSClientManager();

// Add client configurations
manager.AddClient("default", new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.1.100:22122" }
});

manager.AddClient("backup", new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.2.100:22122" }
});

// Get clients
var defaultClient = manager.GetClient("default");
var backupClient = manager.GetClient("backup");

// Check if client exists
if (manager.HasClient("cdn"))
{
    var cdnClient = manager.GetClient("cdn");
}

// Register client dynamically
manager.RegisterClient("new-cluster", new FastDFSConfiguration
{
    TrackerServers = new[] { "192.168.3.100:22122" }
});

// Remove client
manager.RemoveClient("backup");

// Dispose manager (disposes all clients)
manager.Dispose();
```

## API Overview

### Upload Operations

```csharp
// Upload from byte array (auto-select group)
string fileId = await client.UploadAsync(null, bytes, "jpg", CancellationToken.None);
// Returns: "group1/M00/00/00/wKgBaGVlYWRlYS5qcGc"

// Upload to specific group
string fileId = await client.UploadAsync("group1", bytes, "jpg", CancellationToken.None);

// Upload from stream
string fileId = await client.UploadAsync(null, stream, "pdf", CancellationToken.None);

// Upload from file path
string fileId = await client.UploadFileAsync(null, "/path/to/file.png", CancellationToken.None);

// Upload appender file (supports append later)
string fileId = await client.UploadAppenderFileAsync(null, bytes, "log", CancellationToken.None);

// Append to appender file
await client.AppendFileAsync(fileId, newBytes, CancellationToken.None);
```

### Download Operations

```csharp
// Download to byte array
byte[] content = await client.DownloadAsync(fileId, CancellationToken.None);

// Download to stream
await client.DownloadAsync(fileId, outputStream, CancellationToken.None);

// Download to file
await client.DownloadFileAsync(fileId, "/save/path.jpg", CancellationToken.None);

// Partial download (offset and length)
byte[] partial = await client.DownloadAsync(fileId, 1024, 2048, CancellationToken.None);
```

### File Management

```csharp
// Query file info
FastDFSFileInfo info = await client.QueryFileInfoAsync(fileId, CancellationToken.None);
Console.WriteLine($"Size: {info.FileSize}, CRC32: {info.Crc32}");

// Check if file exists
bool exists = await client.FileExistsAsync(fileId, CancellationToken.None);

// Delete file
await client.DeleteAsync(fileId, CancellationToken.None);

// Set metadata
var metadata = new FastDFSMetadata
{
    { "author", "John" },
    { "created", "2024-01-01" },
    { "width", "1920" },
    { "height", "1080" }
};
await client.SetMetadataAsync(fileId, metadata, MetadataFlag.Overwrite, CancellationToken.None);

// Get metadata
FastDFSMetadata metadata = await client.GetMetadataAsync(fileId, CancellationToken.None);
string author = metadata["author"];
```

## Advanced Usage

### Connection Pool Configuration

```csharp
services.AddFastDFS(options =>
{
    options.TrackerServers = new[] { "192.168.1.100:22122", "192.168.1.101:22122" };
    options.ConnectionPool = new ConnectionPoolConfiguration
    {
        MaxConnectionPerServer = 50,        // Maximum connections per server
        MinConnectionPerServer = 5,         // Minimum connections (pre-warmed)
        ConnectionIdleTimeout = 300,        // Idle timeout in seconds
        ConnectionLifetime = 3600,          // Max lifetime in seconds
        ConnectionTimeout = 30000,          // Connection timeout in ms
        SendTimeout = 30000,                // Send timeout in ms
        ReceiveTimeout = 30000              // Receive timeout in ms
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

    // Choose storage selection strategy:
    options.StorageSelectionStrategy = StorageSelectionStrategy.TrackerSelection; // Default, let tracker decide
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

    // Configure HTTP access
    options.HttpConfig = new HttpConfiguration
    {
        // Option 1: Configure HTTP server URLs for each group
        ServerUrls = new Dictionary<string, string>
        {
            { "group1", "http://img1.example.com" },
            { "group2", "http://img2.example.com" }
        },

        // Option 2: Use template with storage server IP (if not using separate HTTP domain)
        // DefaultServerUrlTemplate = "http://{ip}:8080",  // {ip} will be replaced with storage IP

        // Anti-steal token configuration (optional, requires Nginx module setup)
        AntiStealTokenEnabled = true,
        SecretKey = "your-secret-key-here",         // Must match Nginx configuration
        DefaultTokenExpireSeconds = 3600             // Token valid for 1 hour
    };
});
```

**Generate HTTP URLs:**

```csharp
// Simple HTTP URL
string url = await client.GetFileUrlAsync(fileId, null, CancellationToken.None);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg

// HTTP URL with custom download filename
string url = await client.GetFileUrlAsync(fileId, "my-photo.jpg", CancellationToken.None);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?attname=my-photo.jpg

// Secure URL with anti-steal token (1 hour expiration)
string secureUrl = await client.GetFileUrlWithTokenAsync(fileId, 3600, null, CancellationToken.None);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?token=abc123&ts=1234567890

// Secure URL with custom filename
string secureUrl = await client.GetFileUrlWithTokenAsync(fileId, 3600, "photo.jpg", CancellationToken.None);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?token=abc123&ts=1234567890&attname=photo.jpg
```

**Usage scenarios:**
- Generate URLs for browser direct access
- Integrate with CDN for faster delivery
- Secure file access with time-limited tokens
- Custom download filenames for better user experience

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

// Logs will automatically include:
// - Connection pool events (creation, reuse, disposal)
// - Tracker failover events
// - Upload/download operations
// - Network errors
```

### Error Handling

```csharp
try
{
    var fileId = await client.UploadAsync(null, bytes, "jpg", CancellationToken.None);
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
        _backupClient = factory.GetClient("backup");
        _logger = logger;
    }

    public async Task<string> UploadWithFailover(byte[] content, string extension)
    {
        try
        {
            return await _primaryClient.UploadAsync(null, content, "jpg", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary cluster failed, using backup");
            return await _backupClient.UploadAsync(null, content, "jpg", CancellationToken.None);
        }
    }
}
```

## Performance Tips

1. **Use Connection Pooling**: Always use the built-in connection pool instead of creating new clients for each operation
2. **Reuse Clients**: IFastDFSClient instances are thread-safe and should be reused
3. **Adjust Pool Size**: Tune `MaxConnectionPerServer` based on your workload
4. **Enable Logging**: Use logging to monitor connection pool efficiency
5. **Use Async Operations**: All operations are async - use `await` properly to avoid blocking threads
6. **Shared Connection Pool**: Multiple named clients pointing to the same cluster automatically share a single connection pool — no extra configuration needed. This is especially useful in dynamic registration scenarios where different business entities use the same underlying FastDFS cluster.

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
│   ├── FastDFS.Client/                    # Core library (zero dependencies)
│   │   ├── Protocol/                      # FastDFS protocol implementation
│   │   ├── Connection/                    # Socket connection and pooling
│   │   ├── Tracker/                       # Tracker client
│   │   ├── Storage/                       # Storage client
│   │   ├── Configuration/                 # Configuration models
│   │   ├── Exceptions/                    # Custom exceptions
│   │   └── Utilities/                     # Helper utilities
│   └── FastDFS.Client.DependencyInjection/ # DI extensions
├── tests/
│   └── FastDFS.Client.Tests/              # Unit tests (86+ tests)
└── samples/
    └── FastDFS.Client.Samples/            # Usage examples
```

## Architecture

See [CLAUDE.md](CLAUDE.md) for detailed architecture documentation and development guidelines.

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
