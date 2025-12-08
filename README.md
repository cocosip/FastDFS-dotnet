# FastDFS.Client

A high-performance FastDFS .NET client library with built-in connection pooling and multi-cluster support.

## Features

- ✅ Target framework: **netstandard2.0** (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- ✅ **Connection pooling** for optimal performance
- ✅ **Multi-cluster support** with named clients
- ✅ Full **async/await** support
- ✅ **Dependency injection** integration with IOptions pattern
- ✅ Non-DI factory pattern support
- ✅ Built-in logging with `Microsoft.Extensions.Logging`

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
        return await _client.UploadAsync(content, extension);
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
// Upload from byte array
string fileId = await client.UploadAsync(bytes, ".jpg");

// Upload from stream
string fileId = await client.UploadAsync(stream, ".pdf");

// Upload from file path
string fileId = await client.UploadFileAsync("/path/to/file.png");

// Upload appender file
string fileId = await client.UploadAppenderFileAsync(bytes, ".log");

// Append to file
await client.AppendFileAsync(fileId, newBytes);
```

### Download Operations

```csharp
// Download to byte array
byte[] content = await client.DownloadAsync(fileId);

// Download to stream
await client.DownloadAsync(fileId, outputStream);

// Download to file
await client.DownloadToFileAsync(fileId, "/save/path.jpg");

// Download with offset and length
byte[] partial = await client.DownloadAsync(fileId, offset: 1024, length: 2048);
```

### File Management

```csharp
// Query file info
FastDFSFileInfo info = await client.QueryFileInfoAsync(fileId);
// info.FileSize, info.CreateTime, info.Crc32

// Delete file
await client.DeleteAsync(fileId);

// Set metadata
var metadata = new Dictionary<string, string>
{
    { "author", "John" },
    { "created", "2024-01-01" }
};
await client.SetMetadataAsync(fileId, metadata, MetadataFlag.Overwrite);

// Get metadata
var metadata = await client.GetMetadataAsync(fileId);
```

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

## Architecture

See [CLAUDE.md](CLAUDE.md) for detailed architecture documentation.

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
