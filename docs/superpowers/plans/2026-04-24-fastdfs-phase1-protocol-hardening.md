# FastDFS Phase 1 Protocol Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden the upload and fetch protocol path by introducing shared protocol guards, explicit fixed-width validation, and empty-extension support without changing the public FastDFS client surface.

**Architecture:** Add small internal protocol and domain helpers first, then migrate the upload and fetch request/response path onto those helpers. Keep transport and pooling untouched, keep public method signatures stable, and move correctness rules out of `FastDFSClient` and individual packet classes into dedicated internal components.

**Tech Stack:** C#, netstandard2.0, xUnit, FluentAssertions, existing FastDFS protocol request/response model

---

### Task 1: Add Shared Protocol Field Length Guard

**Files:**
- Create: `src/FastDFS.Client/Protocol/Encoding/ProtocolFieldLengthGuard.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Encoding/ProtocolFieldLengthGuardTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Encoding/ProtocolFieldLengthGuardTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Protocol.Encoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Encoding
{
    public class ProtocolFieldLengthGuardTests
    {
        [Fact]
        public void EnsureUtf8FitsFixedField_WithExactBoundary_ShouldNotThrow()
        {
            Action act = () => ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("groupName", "group1", 16);

            act.Should().NotThrow();
        }

        [Fact]
        public void EnsureUtf8FitsFixedField_WithOversizedAsciiValue_ShouldThrowArgumentException()
        {
            Action act = () => ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("groupName", "12345678901234567", 16);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*groupName*16*");
        }

        [Fact]
        public void EnsureUtf8FitsFixedField_WithNullValue_ShouldThrowArgumentNullException()
        {
            Action act = () => ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("fileExtension", null!, 6);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("value");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolFieldLengthGuardTests" -v minimal`
Expected: FAIL with missing type or namespace errors for `ProtocolFieldLengthGuard`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;
using System.Text;

namespace FastDFS.Client.Protocol.Encoding
{
    internal static class ProtocolFieldLengthGuard
    {
        public static void EnsureUtf8FitsFixedField(string fieldName, string value, int maxBytes)
        {
            if (fieldName == null)
                throw new ArgumentNullException(nameof(fieldName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes), "Max bytes must be greater than 0.");

            int byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > maxBytes)
            {
                throw new ArgumentException(
                    $"Value for '{fieldName}' exceeds the FastDFS fixed field limit of {maxBytes} bytes when UTF-8 encoded.",
                    nameof(value));
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolFieldLengthGuardTests" -v minimal`
Expected: PASS with 3 passing tests.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Encoding/ProtocolFieldLengthGuard.cs tests/FastDFS.Client.Tests/Protocol/Encoding/ProtocolFieldLengthGuardTests.cs
git commit -m "test(protocol): add fixed field length guard"
```

### Task 2: Add Shared Protocol Field Writer

**Files:**
- Create: `src/FastDFS.Client/Protocol/Encoding/ProtocolFieldWriter.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Encoding/ProtocolFieldWriterTests.cs`
- Modify: `src/FastDFS.Client/Protocol/Encoding/ProtocolFieldLengthGuard.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Encoding/ProtocolFieldWriterTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FastDFS.Client.Protocol.Encoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Encoding
{
    public class ProtocolFieldWriterTests
    {
        [Fact]
        public void WriteFixedUtf8_WithEmptyString_ShouldZeroFillTheField()
        {
            var buffer = new byte[6];

            ProtocolFieldWriter.WriteFixedUtf8(buffer, 0, 6, "fileExtension", string.Empty);

            buffer.Should().Equal(new byte[] { 0, 0, 0, 0, 0, 0 });
        }

        [Fact]
        public void WriteFixedUtf8_WithShortAsciiValue_ShouldWriteAndPad()
        {
            var buffer = new byte[6];

            ProtocolFieldWriter.WriteFixedUtf8(buffer, 0, 6, "fileExtension", "jpg");

            buffer[0].Should().Be((byte)'j');
            buffer[1].Should().Be((byte)'p');
            buffer[2].Should().Be((byte)'g');
            buffer[3].Should().Be(0);
            buffer[4].Should().Be(0);
            buffer[5].Should().Be(0);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolFieldWriterTests" -v minimal`
Expected: FAIL with missing type or namespace errors for `ProtocolFieldWriter`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;
using System.Text;

namespace FastDFS.Client.Protocol.Encoding
{
    internal static class ProtocolFieldWriter
    {
        public static void WriteFixedUtf8(byte[] buffer, int offset, int length, string fieldName, string value)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length <= 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField(fieldName, value, length);

            Array.Clear(buffer, offset, length);

            if (value.Length == 0)
                return;

            Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, offset);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolFieldWriterTests" -v minimal`
Expected: PASS with 2 passing tests.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Encoding/ProtocolFieldWriter.cs tests/FastDFS.Client.Tests/Protocol/Encoding/ProtocolFieldWriterTests.cs
git commit -m "feat(protocol): add shared fixed field writer"
```

### Task 3: Introduce FileExtension Value Object and Empty-Extension Semantics

**Files:**
- Create: `src/FastDFS.Client/Domain/Identifiers/FileExtension.cs`
- Create: `tests/FastDFS.Client.Tests/Domain/Identifiers/FileExtensionTests.cs`
- Modify: `src/FastDFS.Client/FastDFSClient.cs`
- Modify: `tests/FastDFS.Client.Tests/FastDFSClientTests.cs`
- Test: `tests/FastDFS.Client.Tests/Domain/Identifiers/FileExtensionTests.cs`
- Test: `tests/FastDFS.Client.Tests/FastDFSClientTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class FileExtensionTests
    {
        [Fact]
        public void Create_WithNull_ShouldThrowArgumentNullException()
        {
            Action act = () => FileExtension.Create(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Create_WithLeadingDot_ShouldNormalizeWithoutDot()
        {
            var extension = FileExtension.Create(".jpg");

            extension.Value.Should().Be("jpg");
        }

        [Fact]
        public void Create_WithEmptyString_ShouldRemainEmpty()
        {
            var extension = FileExtension.Create(string.Empty);

            extension.Value.Should().BeEmpty();
        }
    }
}
```

```csharp
[Fact]
public async Task UploadFileAsync_WithExtensionlessLocalFile_ShouldPassEmptyExtensionToStorageClient()
{
    string tempDirectory = CreateTempDirectory();
    try
    {
        string localFilePath = Path.Combine(tempDirectory, "README");
        await File.WriteAllTextAsync(localFilePath, "payload");
        var storage = new CapturingStorageClient();

        using var client = CreateClient(storage);

        await client.UploadFileAsync("group1", localFilePath);

        storage.LastFileExtension.Should().Be(string.Empty);
    }
    finally
    {
        Directory.Delete(tempDirectory, recursive: true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~FileExtensionTests|FullyQualifiedName~UploadFileAsync_WithExtensionlessLocalFile_ShouldPassEmptyExtensionToStorageClient" -v minimal`
Expected: FAIL with missing type errors for `FileExtension` and an argument validation failure in `FastDFSClient`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct FileExtension
    {
        private FileExtension(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static FileExtension Create(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.StartsWith(".", StringComparison.Ordinal))
                value = value.Substring(1);

            return new FileExtension(value);
        }
    }
}
```

```csharp
var normalizedExtension = Domain.Identifiers.FileExtension.Create(fileExtension);
```

```csharp
var fileExtension = Path.GetExtension(localFilePath);
using var fileStream = File.OpenRead(localFilePath);
return await UploadAsync(groupName, fileStream, fileExtension ?? string.Empty, cancellationToken).ConfigureAwait(false);
```

```csharp
var normalizedExtension = Domain.Identifiers.FileExtension.Create(fileExtension);

_logger.LogInformation("Uploading file to group '{GroupName}', size={Size} bytes, extension={Extension}",
    groupName ?? "(auto-select)", content.Length, normalizedExtension.Value);

var server = await SelectStorageForUploadAsync(groupName, cancellationToken).ConfigureAwait(false);
var fileId = await _storageClient.UploadAsync(server, content, normalizedExtension.Value, cancellationToken).ConfigureAwait(false);
```

```csharp
private sealed class CapturingStorageClient : IStorageClient
{
    public string? LastFileExtension { get; private set; }

    public Task<string> UploadAsync(StorageServerInfo server, Stream contentStream, long contentLength, string fileExtension, CancellationToken cancellationToken = default)
    {
        LastFileExtension = fileExtension;
        return Task.FromResult("group1/M00/00/00/file");
    }

    public Task<string> UploadAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string> UploadAppenderFileAsync(StorageServerInfo server, byte[] content, string fileExtension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AppendFileAsync(StorageServerInfo server, string fileName, byte[] content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<byte[]> DownloadAsync(StorageServerInfo server, string groupName, string fileName, long offset, long length, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DownloadAsync(StorageServerInfo server, string groupName, string fileName, Stream destination, long offset, long length, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FastDFSFileInfo> QueryFileInfoAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SetMetadataAsync(StorageServerInfo server, string groupName, string fileName, FastDFSMetadata metadata, MetadataFlag flag, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FastDFSMetadata> GetMetadataAsync(StorageServerInfo server, string groupName, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~FileExtensionTests|FullyQualifiedName~UploadFileAsync_WithExtensionlessLocalFile_ShouldPassEmptyExtensionToStorageClient" -v minimal`
Expected: PASS with all selected tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Domain/Identifiers/FileExtension.cs src/FastDFS.Client/FastDFSClient.cs tests/FastDFS.Client.Tests/Domain/Identifiers/FileExtensionTests.cs tests/FastDFS.Client.Tests/FastDFSClientTests.cs
git commit -m "feat(client): allow empty file extensions"
```

### Task 4: Migrate Upload Request Types to Shared Protocol Helpers

**Files:**
- Modify: `src/FastDFS.Client/Protocol/Requests/UploadFileRequest.cs`
- Modify: `src/FastDFS.Client/Protocol/Requests/UploadAppenderFileRequest.cs`
- Modify: `tests/FastDFS.Client.Tests/Protocol/Requests/UploadFileRequestTests.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Requests/UploadAppenderFileRequestTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Requests/UploadFileRequestTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Requests/UploadAppenderFileRequestTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Encode_WithEmptyExtension_ShouldZeroFillExtensionField()
{
    var request = new UploadFileRequest
    {
        StorePathIndex = 0,
        FileContent = new byte[] { 0x01 },
        FileExtension = string.Empty
    };

    byte[] encoded = request.Encode();

    encoded[19].Should().Be(0);
    encoded[20].Should().Be(0);
    encoded[21].Should().Be(0);
    encoded[22].Should().Be(0);
    encoded[23].Should().Be(0);
    encoded[24].Should().Be(0);
}

[Fact]
public void Encode_WithTooLongExtension_ShouldThrowArgumentException()
{
    var request = new UploadFileRequest
    {
        StorePathIndex = 0,
        FileContent = new byte[] { 0x01 },
        FileExtension = "verylongext"
    };

    Action act = () => request.Encode();

    act.Should().Throw<ArgumentException>();
}
```

```csharp
using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    public class UploadAppenderFileRequestTests
    {
        [Fact]
        public void Encode_WithEmptyExtension_ShouldZeroFillExtensionField()
        {
            var request = new UploadAppenderFileRequest
            {
                StorePathIndex = 0,
                FileContent = new byte[] { 0x02 },
                FileExtension = string.Empty
            };

            byte[] encoded = request.Encode();

            encoded[19].Should().Be(0);
            encoded[24].Should().Be(0);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~UploadFileRequestTests|FullyQualifiedName~UploadAppenderFileRequestTests" -v minimal`
Expected: FAIL because the current upload request still truncates oversized extensions.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using FastDFS.Client.Domain.Identifiers;
using FastDFS.Client.Protocol.Encoding;
```

```csharp
var extension = Domain.Identifiers.FileExtension.Create(this.FileExtension).Value;

var headerSize = 1 + 8 + FastDFSConstants.FileExtNameMaxLength;
var body = new byte[headerSize + FileContent.Length];

int offset = 0;
body[offset] = StorePathIndex;
offset += 1;

ByteConverter.WriteInt64(FileContent.Length, body, offset);
offset += 8;

ProtocolFieldWriter.WriteFixedUtf8(body, offset, FastDFSConstants.FileExtNameMaxLength, "fileExtension", extension);
offset += FastDFSConstants.FileExtNameMaxLength;

Array.Copy(FileContent, 0, body, offset, FileContent.Length);
```

Apply the same change pattern to `UploadAppenderFileRequest`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~UploadFileRequestTests|FullyQualifiedName~UploadAppenderFileRequestTests" -v minimal`
Expected: PASS with upload request tests updated for empty-extension support and explicit oversize rejection.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Requests/UploadFileRequest.cs src/FastDFS.Client/Protocol/Requests/UploadAppenderFileRequest.cs tests/FastDFS.Client.Tests/Protocol/Requests/UploadFileRequestTests.cs tests/FastDFS.Client.Tests/Protocol/Requests/UploadAppenderFileRequestTests.cs
git commit -m "refactor(protocol): harden upload request encoding"
```

### Task 5: Add GroupName and FastDFSFileId Domain Types for Fetch Path

**Files:**
- Create: `src/FastDFS.Client/Domain/Identifiers/GroupName.cs`
- Create: `src/FastDFS.Client/Domain/Identifiers/FastDFSFileId.cs`
- Create: `tests/FastDFS.Client.Tests/Domain/Identifiers/GroupNameTests.cs`
- Create: `tests/FastDFS.Client.Tests/Domain/Identifiers/FastDFSFileIdTests.cs`
- Modify: `src/FastDFS.Client/Utilities/FileIdHelper.cs`
- Test: `tests/FastDFS.Client.Tests/Domain/Identifiers/GroupNameTests.cs`
- Test: `tests/FastDFS.Client.Tests/Domain/Identifiers/FastDFSFileIdTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class GroupNameTests
    {
        [Fact]
        public void Create_WithOversizedUtf8Value_ShouldThrowArgumentException()
        {
            Action act = () => GroupName.Create("12345678901234567");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithValidValue_ShouldPreserveValue()
        {
            var groupName = GroupName.Create("group1");

            groupName.Value.Should().Be("group1");
        }
    }
}
```

```csharp
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class FastDFSFileIdTests
    {
        [Fact]
        public void Parse_WithFullFileId_ShouldSplitGroupAndFileName()
        {
            var fileId = FastDFSFileId.Parse("group1/M00/00/00/file.txt", null);

            fileId.GroupName.Value.Should().Be("group1");
            fileId.FileName.Should().Be("M00/00/00/file.txt");
        }

        [Fact]
        public void Parse_WithStoragePathAndDefaultGroup_ShouldUseDefaultGroup()
        {
            var fileId = FastDFSFileId.Parse("M00/00/00/file.txt", "group1");

            fileId.GroupName.Value.Should().Be("group1");
            fileId.FileName.Should().Be("M00/00/00/file.txt");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~GroupNameTests|FullyQualifiedName~FastDFSFileIdTests" -v minimal`
Expected: FAIL with missing domain identifier types.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Encoding;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct GroupName
    {
        private GroupName(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static GroupName Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(value));

            ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("groupName", value, FastDFSConstants.GroupNameMaxLength);
            return new GroupName(value);
        }
    }
}
```

```csharp
using System;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct FastDFSFileId
    {
        private FastDFSFileId(GroupName groupName, string fileName)
        {
            GroupName = groupName;
            FileName = fileName;
        }

        public GroupName GroupName { get; }
        public string FileName { get; }

        public static FastDFSFileId Parse(string fileId, string? defaultGroupName)
        {
            FileIdHelper.ParseFileId(fileId, out string groupName, out string fileName, defaultGroupName);
            return new FastDFSFileId(GroupName.Create(groupName), fileName);
        }
    }
}
```

Keep `FileIdHelper` behavior unchanged in this task; only ensure the new domain types exist for later request migration.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~GroupNameTests|FullyQualifiedName~FastDFSFileIdTests" -v minimal`
Expected: PASS with 4 passing tests.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Domain/Identifiers/GroupName.cs src/FastDFS.Client/Domain/Identifiers/FastDFSFileId.cs tests/FastDFS.Client.Tests/Domain/Identifiers/GroupNameTests.cs tests/FastDFS.Client.Tests/Domain/Identifiers/FastDFSFileIdTests.cs
git commit -m "feat(domain): add fetch path identifier types"
```

### Task 6: Migrate Download and Query Fetch Requests to Explicit Group Validation

**Files:**
- Modify: `src/FastDFS.Client/Protocol/Requests/DownloadFileRequest.cs`
- Modify: `src/FastDFS.Client/Protocol/Requests/QueryFetchRequest.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Requests/DownloadFileRequestTests.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Requests/QueryFetchRequestTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Requests/DownloadFileRequestTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Requests/QueryFetchRequestTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    public class DownloadFileRequestTests
    {
        [Fact]
        public void Encode_WithOversizedGroupName_ShouldThrowArgumentException()
        {
            var request = new DownloadFileRequest
            {
                GroupName = "12345678901234567",
                FileName = "M00/00/00/file.txt"
            };

            Action act = () => request.Encode();

            act.Should().Throw<ArgumentException>();
        }
    }
}
```

```csharp
using System;
using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    public class QueryFetchRequestTests
    {
        [Fact]
        public void Encode_WithOversizedGroupName_ShouldThrowArgumentException()
        {
            var request = new QueryFetchRequest
            {
                GroupName = "12345678901234567",
                FileName = "M00/00/00/file.txt"
            };

            Action act = () => request.Encode();

            act.Should().Throw<ArgumentException>();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~DownloadFileRequestTests|FullyQualifiedName~QueryFetchRequestTests" -v minimal`
Expected: FAIL because current request encoding silently truncates `GroupName`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using FastDFS.Client.Domain.Identifiers;
using FastDFS.Client.Protocol.Encoding;
```

```csharp
var groupName = Domain.Identifiers.GroupName.Create(this.GroupName).Value;
var fileNameBytes = Encoding.UTF8.GetBytes(FileName);
var bodyLength = 8 + 8 + FastDFSConstants.GroupNameMaxLength + fileNameBytes.Length;
var body = new byte[bodyLength];

int offset = 0;
ByteConverter.WriteInt64(FileOffset, body, offset);
offset += 8;
ByteConverter.WriteInt64(DownloadBytes, body, offset);
offset += 8;
ProtocolFieldWriter.WriteFixedUtf8(body, offset, FastDFSConstants.GroupNameMaxLength, "groupName", groupName);
offset += FastDFSConstants.GroupNameMaxLength;
Array.Copy(fileNameBytes, 0, body, offset, fileNameBytes.Length);
```

```csharp
var groupName = Domain.Identifiers.GroupName.Create(this.GroupName).Value;
var fileNameBytes = Encoding.UTF8.GetBytes(FileName);
var bodyLength = FastDFSConstants.GroupNameMaxLength + fileNameBytes.Length;
var body = new byte[bodyLength];

ProtocolFieldWriter.WriteFixedUtf8(body, 0, FastDFSConstants.GroupNameMaxLength, "groupName", groupName);
Array.Copy(fileNameBytes, 0, body, FastDFSConstants.GroupNameMaxLength, fileNameBytes.Length);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~DownloadFileRequestTests|FullyQualifiedName~QueryFetchRequestTests" -v minimal`
Expected: PASS with both request tests passing.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Requests/DownloadFileRequest.cs src/FastDFS.Client/Protocol/Requests/QueryFetchRequest.cs tests/FastDFS.Client.Tests/Protocol/Requests/DownloadFileRequestTests.cs tests/FastDFS.Client.Tests/Protocol/Requests/QueryFetchRequestTests.cs
git commit -m "refactor(protocol): reject oversized fetch group names"
```

### Task 7: Add Response Block Guard and Harden Query Store/Fetch Responses

**Files:**
- Create: `src/FastDFS.Client/Protocol/Decoding/ProtocolBlockGuard.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolBlockGuardTests.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryStoreResponseTests.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryFetchResponseTests.cs`
- Modify: `src/FastDFS.Client/Protocol/Responses/QueryStoreResponse.cs`
- Modify: `src/FastDFS.Client/Protocol/Responses/QueryFetchResponse.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolBlockGuardTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryStoreResponseTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryFetchResponseTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol.Decoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Decoding
{
    public class ProtocolBlockGuardTests
    {
        [Fact]
        public void EnsureMinimumBodyLength_WithTooShortBody_ShouldThrowFastDFSProtocolException()
        {
            Action act = () => ProtocolBlockGuard.EnsureMinimumBodyLength("QueryFetchResponse", 38, 39);

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
```

```csharp
using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class QueryStoreResponseTests
    {
        [Fact]
        public void Decode_WithShortBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryStoreResponse();
            var header = new FastDFSHeader(39, 100, 0);

            Action act = () => response.Decode(header, new byte[39]);

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
```

```csharp
using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class QueryFetchResponseTests
    {
        [Fact]
        public void Decode_WithShortBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryFetchResponse();
            var header = new FastDFSHeader(38, 102, 0);

            Action act = () => response.Decode(header, new byte[38]);

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolBlockGuardTests|FullyQualifiedName~QueryStoreResponseTests|FullyQualifiedName~QueryFetchResponseTests" -v minimal`
Expected: FAIL because `ProtocolBlockGuard` does not exist and current responses throw `ArgumentException`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using FastDFS.Client.Exceptions;

namespace FastDFS.Client.Protocol.Decoding
{
    internal static class ProtocolBlockGuard
    {
        public static void EnsureMinimumBodyLength(string responseName, int actualLength, int minimumLength)
        {
            if (actualLength < minimumLength)
            {
                throw new FastDFSProtocolException(
                    $"{responseName} body length {actualLength} is shorter than the minimum expected {minimumLength} bytes.");
            }
        }
    }
}
```

```csharp
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol.Decoding;
```

```csharp
ProtocolBlockGuard.EnsureMinimumBodyLength(nameof(QueryStoreResponse), body?.Length ?? 0, ResponseBodyLength);
```

```csharp
ProtocolBlockGuard.EnsureMinimumBodyLength(nameof(QueryFetchResponse), body?.Length ?? 0, ResponseBodyLength);
```

Leave the rest of the parsing logic unchanged in this phase; only standardize failure mode and minimum-length validation.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolBlockGuardTests|FullyQualifiedName~QueryStoreResponseTests|FullyQualifiedName~QueryFetchResponseTests" -v minimal`
Expected: PASS with all three response and guard test classes passing.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Decoding/ProtocolBlockGuard.cs src/FastDFS.Client/Protocol/Responses/QueryStoreResponse.cs src/FastDFS.Client/Protocol/Responses/QueryFetchResponse.cs tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolBlockGuardTests.cs tests/FastDFS.Client.Tests/Protocol/Responses/QueryStoreResponseTests.cs tests/FastDFS.Client.Tests/Protocol/Responses/QueryFetchResponseTests.cs
git commit -m "refactor(protocol): standardize fetch response guards"
```

### Task 8: Run Phase 1 Regression Sweep

**Files:**
- Modify: `tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj` (only if new folders need explicit include updates)
- Test: `tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj`

- [ ] **Step 1: Run the focused request and response suite**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~UploadFileRequestTests|FullyQualifiedName~UploadAppenderFileRequestTests|FullyQualifiedName~DownloadFileRequestTests|FullyQualifiedName~QueryFetchRequestTests|FullyQualifiedName~QueryStoreResponseTests|FullyQualifiedName~QueryFetchResponseTests|FullyQualifiedName~FastDFSClientTests|FullyQualifiedName~FileExtensionTests|FullyQualifiedName~GroupNameTests|FullyQualifiedName~FastDFSFileIdTests|FullyQualifiedName~ProtocolFieldLengthGuardTests|FullyQualifiedName~ProtocolFieldWriterTests|FullyQualifiedName~ProtocolBlockGuardTests" -v minimal`
Expected: PASS with all selected tests green.

- [ ] **Step 2: Run the full test project**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj -v minimal`
Expected: PASS for the entire unit test project.

- [ ] **Step 3: Review changed files for Phase 1 scope control**

Run: `git diff --stat HEAD~7..HEAD`
Expected: Only Phase 1 protocol/domain files and related tests appear; no DI factory or metadata-wide cleanup yet.

- [ ] **Step 4: Commit the final validation checkpoint**

```bash
git add src/FastDFS.Client tests/FastDFS.Client.Tests
git commit -m "test(protocol): validate phase 1 hardening sweep"
```

## Self-Review

### Spec Coverage

- Phase 1 upload path coverage: Tasks 1-4.
- Phase 1 fetch request and response coverage: Tasks 5-7.
- Phase 1 regression coverage: Task 8.
- Phase 2 management responses: intentionally excluded from this plan.
- Phase 3 metadata and DI registry cleanup: intentionally excluded from this plan.

### Placeholder Scan

- No deferred placeholders or incomplete implementation markers remain in tasks.
- Every code-changing step includes concrete code to add or adapt.
- Every validation step includes an exact command and expected outcome.

### Type Consistency

- `ProtocolFieldLengthGuard` is introduced before `ProtocolFieldWriter`.
- `FileExtension` is introduced before upload request migration depends on it.
- `GroupName` and `FastDFSFileId` are introduced before fetch request migration depends on them.
- `ProtocolBlockGuard` is introduced before response migration depends on it.
