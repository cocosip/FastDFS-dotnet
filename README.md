# FastDFS.Client

A modern, high-performance FastDFS .NET client library with built-in connection pooling and multi-cluster support.

[![NuGet](https://img.shields.io/nuget/v/FastDFS.Client.svg)](https://www.nuget.org/packages/FastDFS.Client/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Features

- ✅ **High Performance**: Uses modern Socket API instead of TcpClient for better throughput and lower latency
- ✅ **Target Framework**: netstandard2.0 (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- ✅ **Connection Pooling**: Automatic connection management with configurable pool sizes
- ✅ **Multi-Cluster Support**: Manage multiple FastDFS clusters with named clients
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
        _backupClient = factory.GetClient("backup");
    }
}
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

// Upload file
var fileId = await client.UploadAsync(fileBytes, ".jpg");

// Download file
var content = await client.DownloadAsync(fileId);

// Delete file
await client.DeleteAsync(fileId);
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
string url = await client.GetFileUrlAsync(fileId);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg

// HTTP URL with custom download filename
string url = await client.GetFileUrlAsync(fileId, attachmentFilename: "my-photo.jpg");
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?attname=my-photo.jpg

// Secure URL with anti-steal token (1 hour expiration)
string secureUrl = await client.GetFileUrlWithTokenAsync(fileId, expireSeconds: 3600);
// Result: http://img1.example.com/group1/M00/00/00/xxxxx.jpg?token=abc123&ts=1234567890

// Secure URL with custom filename
string secureUrl = await client.GetFileUrlWithTokenAsync(fileId, expireSeconds: 3600, attachmentFilename: "photo.jpg");
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
        _backupClient = factory.GetClient("backup");
        _logger = logger;
    }

    public async Task<string> UploadWithFailover(byte[] content, string extension)
    {
        try
        {
            return await _primaryClient.UploadAsync(null, content, extension);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary cluster failed, using backup");
            return await _backupClient.UploadAsync(null, content, extension);
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
