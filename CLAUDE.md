# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a FastDFS .NET client library targeting netstandard2.0 with built-in connection pooling support. FastDFS is a distributed file system designed for high-capacity storage and load balancing.

**Key Features:**
- Multi-cluster support (named clients)
- Connection pooling for optimal performance
- Async/await support
- Dependency injection (DI) integration with IOptions pattern
- Non-DI factory pattern support

## Building and Testing

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Build in Release mode
dotnet build -c Release

# Pack NuGet package
dotnet pack -c Release
```

## Architecture

### FastDFS Protocol Implementation

FastDFS uses a binary protocol over TCP with the following components:

- **Tracker Server**: Directory service that manages storage servers
- **Storage Server**: Stores and retrieves files
- **Client**: Connects to tracker to get storage server info, then uploads/downloads files

Protocol packet structure:
- Header: 10 bytes (8 bytes body length + 1 byte command + 1 byte status)
- Body: Variable length based on command type

### Core Components

**Connection Pool**
- Manages persistent TCP connections to tracker and storage servers
- Implements connection lifecycle (creation, validation, recycling)
- Supports configurable pool sizes (min/max connections, idle timeout)
- Thread-safe connection acquisition and release

**Protocol Layer**
- Handles FastDFS binary protocol encoding/decoding
- Command definitions (upload, download, delete, query, etc.)
- Packet serialization/deserialization
- Network communication over TCP streams

**Client API**
- High-level API for file operations (upload, download, delete, query metadata)
- Async/await support for all I/O operations
- Automatic tracker server failover
- Support for file metadata and custom attributes

**Configuration**
- Tracker server endpoints (supports multiple trackers)
- Connection pool settings
- Network timeouts
- Charset encoding (typically UTF-8)

### Key Design Patterns

- **Object Pooling**: Reusable connection objects to minimize TCP handshake overhead
- **Factory Pattern**: Connection creation and initialization
- **Command Pattern**: FastDFS protocol commands encapsulated as objects
- **Strategy Pattern**: Different storage strategies (group/path selection)

## FastDFS Protocol Commands

Common commands to implement:
- `TRACKER_PROTO_CMD_SERVICE_QUERY_STORE_WITHOUT_GROUP_ONE` (101): Query storage server for upload
- `TRACKER_PROTO_CMD_SERVICE_QUERY_FETCH_ONE` (102): Query storage server for download
- `STORAGE_PROTO_CMD_UPLOAD_FILE` (11): Upload file to storage
- `STORAGE_PROTO_CMD_DOWNLOAD_FILE` (14): Download file from storage
- `STORAGE_PROTO_CMD_DELETE_FILE` (12): Delete file
- `STORAGE_PROTO_CMD_QUERY_FILE_INFO` (22): Query file metadata
- `STORAGE_PROTO_CMD_SET_METADATA` (13): Set file metadata
- `STORAGE_PROTO_CMD_GET_METADATA` (15): Get file metadata

## Connection Pool Implementation Considerations

- Use `System.Buffers.ArrayPool<byte>` for buffer pooling to reduce GC pressure
- Implement health checks to detect stale connections
- Support graceful shutdown with connection draining
- Consider using `System.Threading.Channels` for connection queue management
- Implement exponential backoff for connection retry logic

## Error Handling

FastDFS error codes are returned in the response packet status byte:
- 0: Success
- 2: ENOENT (file not found)
- 22: EINVAL (invalid argument)
- 28: ENOSPC (no space left)
- Other codes map to standard errno values

## Testing Strategy

- Unit tests for protocol encoding/decoding
- Integration tests require actual FastDFS server (use Docker for CI/CD)
- Connection pool tests should verify concurrent access patterns
- Mock tracker/storage responses for client API tests
- Performance tests for connection pool efficiency

## Dependencies

Target netstandard2.0 for broad compatibility (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)

Consider these packages:
- `Microsoft.Extensions.ObjectPool` (optional, for connection pooling abstractions)
- `Microsoft.Extensions.Logging.Abstractions` (for logging integration)
- `System.Buffers` (included in netstandard2.0 for efficient memory management)

## File Naming Convention

FastDFS file IDs have format: `group_name/M00/xx/xx/xxxxxxxxxx.extension`
- group_name: Storage group identifier
- M00: Store path index
- xx/xx: Two-level directory structure
- xxxxxxxxxx: Base64-encoded file hash + timestamp + server info

## Naming Conventions

**IMPORTANT: Use correct casing for FastDFS**
- Correct: `FastDFS`, `FastDFSClient`, `FastDFSOptions`, `IFastDFSClient`
- Incorrect: `FastDfs`, `Fastdfs`, `FastDfs`

The acronym "DFS" should always be fully capitalized as it stands for "Distributed File System".

## Multi-Cluster Architecture

The SDK supports multiple FastDFS clusters in a single application:

**DI Registration:**
```csharp
// Single cluster (default)
services.AddFastDFS(options => {
    options.TrackerServers = new[] { "192.168.1.100:22122" };
});

// Multiple clusters (named)
services.AddFastDFS("default", options => { /* config */ });
services.AddFastDFS("backup", options => { /* config */ });

// From configuration
services.AddFastDFS(configuration.GetSection("FastDFS"));
```

**Usage:**
```csharp
// Single cluster - inject IFastDFSClient directly
public MyService(IFastDFSClient client) { }

// Multiple clusters - use factory
public MyService(IFastDFSClientFactory factory) {
    var client1 = factory.GetClient("default");
    var client2 = factory.GetClient("backup");
}
```

**Configuration structure (appsettings.json):**
```json
{
  "FastDFS": {
    "Clusters": {
      "default": {
        "TrackerServers": ["192.168.1.100:22122"],
        "ConnectionPool": { /* settings */ }
      },
      "backup": {
        "TrackerServers": ["192.168.2.100:22122"],
        "ConnectionPool": { /* settings */ }
      }
    }
  }
}
```

Each named cluster has its own independent connection pool.
