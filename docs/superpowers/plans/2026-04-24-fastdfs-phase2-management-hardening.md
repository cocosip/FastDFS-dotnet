# FastDFS Phase 2 Management and Batch Protocol Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden the management and batch tracker path by introducing shared fixed-width field reading, explicit block-shape validation, and management-path identifier guards without expanding into Phase 3 metadata or DI cleanup.

**Architecture:** Build the missing protocol decoding helper first, then extend the block guard so batch responses can validate body shape before parsing. Once the shared helpers exist, migrate the management request path and the four management/batch responses onto them, keeping `TrackerClient` behavior stable while standardizing protocol failures.

**Tech Stack:** C#, netstandard2.0, xUnit, FluentAssertions, existing FastDFS request/response model

---

### Task 1: Add Shared Protocol Field Reader

**Files:**
- Create: `src/FastDFS.Client/Protocol/Decoding/ProtocolFieldReader.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolFieldReaderTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolFieldReaderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FastDFS.Client.Protocol.Decoding;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Decoding
{
    public class ProtocolFieldReaderTests
    {
        [Fact]
        public void ReadFixedUtf8_WithNullPadding_ShouldTrimTrailingNulls()
        {
            var buffer = new byte[16];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(buffer, 0);

            string value = ProtocolFieldReader.ReadFixedUtf8(buffer, 0, 16);

            value.Should().Be("group1");
        }

        [Fact]
        public void ReadFixedUtf8_WithOffset_ShouldReadRequestedSegment()
        {
            var buffer = new byte[32];
            System.Text.Encoding.UTF8.GetBytes("storage-a").CopyTo(buffer, 16);

            string value = ProtocolFieldReader.ReadFixedUtf8(buffer, 16, 16);

            value.Should().Be("storage-a");
        }

        [Fact]
        public void ReadFixedUtf8_WithAllZeroBytes_ShouldReturnEmptyString()
        {
            var buffer = new byte[16];

            string value = ProtocolFieldReader.ReadFixedUtf8(buffer, 0, 16);

            value.Should().BeEmpty();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolFieldReaderTests" -v minimal`
Expected: FAIL with missing type or namespace errors for `ProtocolFieldReader`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;
using FastDFS.Client.Utilities;

namespace FastDFS.Client.Protocol.Decoding
{
    internal static class ProtocolFieldReader
    {
        public static string ReadFixedUtf8(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length <= 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return buffer.ReadFixedString(offset, length, System.Text.Encoding.UTF8).TrimEnd('\0');
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolFieldReaderTests" -v minimal`
Expected: PASS with 3 passing tests.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Decoding/ProtocolFieldReader.cs tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolFieldReaderTests.cs
git commit -m "feat(protocol): add shared field reader"
```

### Task 2: Extend Protocol Block Guard for Batch Responses

**Files:**
- Modify: `src/FastDFS.Client/Protocol/Decoding/ProtocolBlockGuard.cs`
- Modify: `tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolBlockGuardTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolBlockGuardTests.cs`

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
        public void EnsureExactBlockMultiple_WithMisalignedLength_ShouldThrowFastDFSProtocolException()
        {
            Action act = () => ProtocolBlockGuard.EnsureExactBlockMultiple("QueryStoreAllResponse", 41, 40);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void ResolveSupportedBlockSize_WithSupported592ByteLength_ShouldReturn592()
        {
            int blockSize = ProtocolBlockGuard.ResolveSupportedBlockSize("ListStorageServersResponse", 1184, new[] { 600, 592 });

            blockSize.Should().Be(592);
        }

        [Fact]
        public void ResolveSupportedBlockSize_WithUnsupportedLength_ShouldThrowFastDFSProtocolException()
        {
            Action act = () => ProtocolBlockGuard.ResolveSupportedBlockSize("ListStorageServersResponse", 601, new[] { 600, 592 });

            act.Should().Throw<FastDFSProtocolException>();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolBlockGuardTests" -v minimal`
Expected: FAIL because the new guard members do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;
using System.Globalization;
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

        public static void EnsureExactBlockMultiple(string responseName, int actualLength, int blockSize)
        {
            if (actualLength % blockSize != 0)
            {
                throw new FastDFSProtocolException(
                    $"{responseName} body length {actualLength} is not a multiple of the expected block size {blockSize} bytes.");
            }
        }

        public static int ResolveSupportedBlockSize(string responseName, int actualLength, int[] supportedBlockSizes)
        {
            if (supportedBlockSizes == null)
                throw new ArgumentNullException(nameof(supportedBlockSizes));

            foreach (int blockSize in supportedBlockSizes)
            {
                if (actualLength % blockSize == 0)
                    return blockSize;
            }

            throw new FastDFSProtocolException(
                $"{responseName} body length {actualLength.ToString(CultureInfo.InvariantCulture)} does not match any supported block size: {string.Join(", ", supportedBlockSizes)}.");
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolBlockGuardTests" -v minimal`
Expected: PASS with all ProtocolBlockGuard tests green.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Decoding/ProtocolBlockGuard.cs tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolBlockGuardTests.cs
git commit -m "refactor(protocol): extend block guard for batch responses"
```

### Task 3: Add StorageServerId Identifier

**Files:**
- Create: `src/FastDFS.Client/Domain/Identifiers/StorageServerId.cs`
- Create: `tests/FastDFS.Client.Tests/Domain/Identifiers/StorageServerIdTests.cs`
- Test: `tests/FastDFS.Client.Tests/Domain/Identifiers/StorageServerIdTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Domain.Identifiers
{
    public class StorageServerIdTests
    {
        [Fact]
        public void Create_WithNull_ShouldThrowArgumentNullException()
        {
            Action act = () => StorageServerId.Create(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Create_WithOversizedUtf8Value_ShouldThrowArgumentException()
        {
            Action act = () => StorageServerId.Create("12345678901234567");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithValidValue_ShouldPreserveValue()
        {
            var serverId = StorageServerId.Create("192.168.0.10");

            serverId.Value.Should().Be("192.168.0.10");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~StorageServerIdTests" -v minimal`
Expected: FAIL with missing type errors for `StorageServerId`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Encoding;

namespace FastDFS.Client.Domain.Identifiers
{
    internal readonly struct StorageServerId
    {
        private StorageServerId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static StorageServerId Create(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Storage server ID cannot be null or empty.", nameof(value));

            ProtocolFieldLengthGuard.EnsureUtf8FitsFixedField("storageServerId", value, FastDFSConstants.StorageIdMaxLength);
            return new StorageServerId(value);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~StorageServerIdTests" -v minimal`
Expected: PASS with 3 passing tests.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Domain/Identifiers/StorageServerId.cs tests/FastDFS.Client.Tests/Domain/Identifiers/StorageServerIdTests.cs
git commit -m "feat(domain): add storage server identifier"
```

### Task 4: Harden ListStorageServersRequest

**Files:**
- Modify: `src/FastDFS.Client/Protocol/Requests/ListStorageServersRequest.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Requests/ListStorageServersRequestTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Requests/ListStorageServersRequestTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Requests;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Requests
{
    public class ListStorageServersRequestTests
    {
        [Fact]
        public void Encode_WithGroupOnly_ShouldWriteSingleFixedField()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "group1"
            };

            byte[] encoded = request.Encode();

            encoded.Should().HaveCount(10 + 16);
            encoded[10].Should().Be((byte)'g');
            encoded[15].Should().Be((byte)'1');
            encoded[25].Should().Be(0);
        }

        [Fact]
        public void Encode_WithGroupAndServerId_ShouldWriteBothFixedFields()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "group1",
                StorageServerId = "192.168.0.10"
            };

            byte[] encoded = request.Encode();

            encoded.Should().HaveCount(10 + 32);
            encoded[26].Should().Be((byte)'1');
            encoded[37].Should().Be((byte)'0');
        }

        [Fact]
        public void Encode_WithOversizedGroupName_ShouldThrowArgumentException()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "12345678901234567"
            };

            Action act = () => request.Encode();

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Encode_WithOversizedStorageServerId_ShouldThrowArgumentException()
        {
            var request = new ListStorageServersRequest
            {
                GroupName = "group1",
                StorageServerId = "12345678901234567"
            };

            Action act = () => request.Encode();

            act.Should().Throw<ArgumentException>();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ListStorageServersRequestTests" -v minimal`
Expected: FAIL because the current request still truncates oversize values and lacks a dedicated test file.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using System;
using FastDFS.Client.Domain.Identifiers;
using FastDFS.Client.Protocol.Encoding;
using FastDFS.Client.Protocol.Responses;
```

```csharp
protected override byte[]? EncodeBody()
{
    var groupName = Domain.Identifiers.GroupName.Create(this.GroupName).Value;
    var hasStorageServerId = !string.IsNullOrWhiteSpace(StorageServerId);
    var bodyLength = hasStorageServerId
        ? FastDFSConstants.GroupNameMaxLength + FastDFSConstants.StorageIdMaxLength
        : FastDFSConstants.GroupNameMaxLength;
    var body = new byte[bodyLength];

    ProtocolFieldWriter.WriteFixedUtf8(body, 0, FastDFSConstants.GroupNameMaxLength, "groupName", groupName);

    if (hasStorageServerId)
    {
        var storageServerId = Domain.Identifiers.StorageServerId.Create(StorageServerId!).Value;
        ProtocolFieldWriter.WriteFixedUtf8(body, FastDFSConstants.GroupNameMaxLength, FastDFSConstants.StorageIdMaxLength, "storageServerId", storageServerId);
    }

    return body;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ListStorageServersRequestTests" -v minimal`
Expected: PASS with 4 passing tests.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Requests/ListStorageServersRequest.cs tests/FastDFS.Client.Tests/Protocol/Requests/ListStorageServersRequestTests.cs
git commit -m "refactor(protocol): harden list storage servers request"
```

### Task 5: Harden QueryStoreAllResponse and QueryFetchAllResponse

**Files:**
- Modify: `src/FastDFS.Client/Protocol/Responses/QueryStoreAllResponse.cs`
- Modify: `src/FastDFS.Client/Protocol/Responses/QueryFetchAllResponse.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryStoreAllResponseTests.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryFetchAllResponseTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryStoreAllResponseTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Responses/QueryFetchAllResponseTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class QueryStoreAllResponseTests
    {
        [Fact]
        public void Decode_WithMisalignedBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryStoreAllResponse();
            var header = new FastDFSHeader(41, 107, 0);

            Action act = () => response.Decode(header, new byte[41]);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void Decode_WithSingleBlock_ShouldParseServer()
        {
            var response = new QueryStoreAllResponse();
            var body = new byte[40];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(body, 0);
            System.Text.Encoding.UTF8.GetBytes("192.168.0.10").CopyTo(body, 16);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(23000, body, 31);
            body[39] = 1;

            response.Decode(new FastDFSHeader(body.Length, 107, 0), body);

            response.ServerInfos.Should().HaveCount(1);
            response.ServerInfos[0].GroupName.Should().Be("group1");
            response.ServerInfos[0].IpAddress.Should().Be("192.168.0.10");
            response.ServerInfos[0].Port.Should().Be(23000);
            response.ServerInfos[0].StorePathIndex.Should().Be(1);
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
    public class QueryFetchAllResponseTests
    {
        [Fact]
        public void Decode_WithMisalignedBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new QueryFetchAllResponse();
            var header = new FastDFSHeader(40, 106, 0);

            Action act = () => response.Decode(header, new byte[40]);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void Decode_WithSingleBlock_ShouldParseServer()
        {
            var response = new QueryFetchAllResponse();
            var body = new byte[39];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(body, 0);
            System.Text.Encoding.UTF8.GetBytes("192.168.0.20").CopyTo(body, 16);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(23000, body, 31);

            response.Decode(new FastDFSHeader(body.Length, 106, 0), body);

            response.ServerInfos.Should().HaveCount(1);
            response.ServerInfos[0].GroupName.Should().Be("group1");
            response.ServerInfos[0].IpAddress.Should().Be("192.168.0.20");
            response.ServerInfos[0].Port.Should().Be(23000);
            response.ServerInfos[0].StorePathIndex.Should().Be(0);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~QueryStoreAllResponseTests|FullyQualifiedName~QueryFetchAllResponseTests" -v minimal`
Expected: FAIL because the current responses throw `ArgumentException` on malformed body length and do not use shared helpers.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using FastDFS.Client.Protocol.Decoding;
```

```csharp
protected override void DecodeBody(byte[]? body)
{
    if (body == null || body.Length == 0)
    {
        ServerInfos = new List<StorageServerInfo>();
        return;
    }

    ProtocolBlockGuard.EnsureMinimumBodyLength(nameof(QueryStoreAllResponse), body.Length, StorageInfoBlockSize);
    ProtocolBlockGuard.EnsureExactBlockMultiple(nameof(QueryStoreAllResponse), body.Length, StorageInfoBlockSize);
    var responseBody = body;

    int storageCount = responseBody.Length / StorageInfoBlockSize;
    ServerInfos = new List<StorageServerInfo>(storageCount);

    for (int i = 0; i < storageCount; i++)
    {
        int offset = i * StorageInfoBlockSize;
        var serverInfo = new StorageServerInfo
        {
            GroupName = ProtocolFieldReader.ReadFixedUtf8(responseBody, offset, FastDFSConstants.GroupNameMaxLength),
            IpAddress = ProtocolFieldReader.ReadFixedUtf8(responseBody, offset + FastDFSConstants.GroupNameMaxLength, FastDFSConstants.IpAddressLength - 1).Trim(),
            Port = (int)ByteConverter.ToInt64(responseBody, offset + 31),
            StorePathIndex = responseBody[offset + 39]
        };

        ServerInfos.Add(serverInfo);
    }
}
```

```csharp
protected override void DecodeBody(byte[]? body)
{
    if (body == null || body.Length == 0)
    {
        ServerInfos = new List<StorageServerInfo>();
        return;
    }

    ProtocolBlockGuard.EnsureMinimumBodyLength(nameof(QueryFetchAllResponse), body.Length, StorageInfoBlockSize);
    ProtocolBlockGuard.EnsureExactBlockMultiple(nameof(QueryFetchAllResponse), body.Length, StorageInfoBlockSize);
    var responseBody = body;

    int storageCount = responseBody.Length / StorageInfoBlockSize;
    ServerInfos = new List<StorageServerInfo>(storageCount);

    for (int i = 0; i < storageCount; i++)
    {
        int offset = i * StorageInfoBlockSize;
        var serverInfo = new StorageServerInfo
        {
            GroupName = ProtocolFieldReader.ReadFixedUtf8(responseBody, offset, FastDFSConstants.GroupNameMaxLength),
            IpAddress = ProtocolFieldReader.ReadFixedUtf8(responseBody, offset + FastDFSConstants.GroupNameMaxLength, FastDFSConstants.IpAddressLength - 1).Trim(),
            Port = (int)ByteConverter.ToInt64(responseBody, offset + 31),
            StorePathIndex = 0
        };

        ServerInfos.Add(serverInfo);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~QueryStoreAllResponseTests|FullyQualifiedName~QueryFetchAllResponseTests" -v minimal`
Expected: PASS with all selected tests green.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Responses/QueryStoreAllResponse.cs src/FastDFS.Client/Protocol/Responses/QueryFetchAllResponse.cs tests/FastDFS.Client.Tests/Protocol/Responses/QueryStoreAllResponseTests.cs tests/FastDFS.Client.Tests/Protocol/Responses/QueryFetchAllResponseTests.cs
git commit -m "refactor(protocol): harden batch storage query responses"
```

### Task 6: Harden ListAllGroupsResponse

**Files:**
- Modify: `src/FastDFS.Client/Protocol/Responses/ListAllGroupsResponse.cs`
- Create: `tests/FastDFS.Client.Tests/Protocol/Responses/ListAllGroupsResponseTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Responses/ListAllGroupsResponseTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using FastDFS.Client.Exceptions;
using FastDFS.Client.Protocol;
using FastDFS.Client.Protocol.Responses;
using FluentAssertions;
using Xunit;

namespace FastDFS.Client.Tests.Protocol.Responses
{
    public class ListAllGroupsResponseTests
    {
        [Fact]
        public void Decode_WithMisalignedBody_ShouldThrowFastDFSProtocolException()
        {
            var response = new ListAllGroupsResponse();
            var header = new FastDFSHeader(106, 91, 0);

            Action act = () => response.Decode(header, new byte[106]);

            act.Should().Throw<FastDFSProtocolException>();
        }

        [Fact]
        public void Decode_WithSingleBlock_ShouldParseGroup()
        {
            var response = new ListAllGroupsResponse();
            var body = new byte[105];
            System.Text.Encoding.UTF8.GetBytes("group1").CopyTo(body, 0);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(1024, body, 16);
            FastDFS.Client.Utilities.ByteConverter.WriteInt64(512, body, 24);

            response.Decode(new FastDFSHeader(body.Length, 91, 0), body);

            response.Groups.Should().HaveCount(1);
            response.Groups[0].GroupName.Should().Be("group1");
            response.Groups[0].TotalMB.Should().Be(1024);
            response.Groups[0].FreeMB.Should().Be(512);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ListAllGroupsResponseTests" -v minimal`
Expected: FAIL because the current response accepts misaligned body lengths by integer division.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using FastDFS.Client.Protocol.Decoding;
```

```csharp
protected override void DecodeBody(byte[]? body)
{
    if (body == null || body.Length == 0)
    {
        Groups = new List<GroupInfo>();
        return;
    }

    ProtocolBlockGuard.EnsureMinimumBodyLength(nameof(ListAllGroupsResponse), body.Length, GroupInfoBlockSize);
    ProtocolBlockGuard.EnsureExactBlockMultiple(nameof(ListAllGroupsResponse), body.Length, GroupInfoBlockSize);
    var responseBody = body;

    int groupCount = responseBody.Length / GroupInfoBlockSize;
    Groups = new List<GroupInfo>(groupCount);

    for (int i = 0; i < groupCount; i++)
    {
        int offset = i * GroupInfoBlockSize;

        var groupInfo = new GroupInfo
        {
            GroupName = ProtocolFieldReader.ReadFixedUtf8(responseBody, offset, 16),
            TotalMB = ByteConverter.ToInt64(responseBody, offset + 16),
            FreeMB = ByteConverter.ToInt64(responseBody, offset + 24),
            TrunkFreeMB = ByteConverter.ToInt64(responseBody, offset + 32),
            StorageServerCount = (int)ByteConverter.ToInt64(responseBody, offset + 40),
            StoragePort = (int)ByteConverter.ToInt64(responseBody, offset + 48),
            StorageHttpPort = (int)ByteConverter.ToInt64(responseBody, offset + 56),
            ActiveServerCount = (int)ByteConverter.ToInt64(responseBody, offset + 64),
            CurrentWriteServer = (int)ByteConverter.ToInt64(responseBody, offset + 72),
            StorePathCount = (int)ByteConverter.ToInt64(responseBody, offset + 80),
            SubdirCountPerPath = (int)ByteConverter.ToInt64(responseBody, offset + 88),
            CurrentTrunkFileId = (int)ByteConverter.ToInt64(responseBody, offset + 96)
        };

        Groups.Add(groupInfo);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ListAllGroupsResponseTests" -v minimal`
Expected: PASS with both tests green.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Responses/ListAllGroupsResponse.cs tests/FastDFS.Client.Tests/Protocol/Responses/ListAllGroupsResponseTests.cs
git commit -m "refactor(protocol): harden list all groups response"
```

### Task 7: Harden ListStorageServersResponse

**Files:**
- Modify: `src/FastDFS.Client/Protocol/Responses/ListStorageServersResponse.cs`
- Modify: `tests/FastDFS.Client.Tests/Protocol/Responses/ListStorageServersResponseTests.cs`
- Test: `tests/FastDFS.Client.Tests/Protocol/Responses/ListStorageServersResponseTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these tests to `ListStorageServersResponseTests`:

```csharp
[Fact]
public void Decode_WithUnsupportedBodyLength_ShouldThrowFastDFSProtocolException()
{
    var response = new ListStorageServersResponse();
    var header = new FastDFSHeader(601, 93, 0);

    Action act = () => response.Decode(header, new byte[601]);

    act.Should().Throw<FastDFSProtocolException>();
}

[Fact]
public void Decode_WithTwo600ByteBlocks_ShouldParseTwoServers()
{
    var response = new ListStorageServersResponse();
    byte[] body = new byte[1200];

    body[0] = (byte)StorageServerStatus.Active;
    WriteFixedString(body, 1, 16, "192.168.0.10");
    WriteFixedString(body, 17, 16, "192.168.0.11");
    WriteFixedString(body, 33, 128, "storage-a.example.com");
    WriteFixedString(body, 161, 6, "6.12");

    int second = 600;
    body[second] = (byte)StorageServerStatus.Offline;
    WriteFixedString(body, second + 1, 16, "192.168.0.20");
    WriteFixedString(body, second + 17, 16, "192.168.0.21");
    WriteFixedString(body, second + 33, 128, "storage-b.example.com");
    WriteFixedString(body, second + 161, 6, "6.11");

    response.Decode(new FastDFSHeader(body.Length, 93, 0), body);

    response.Servers.Should().HaveCount(2);
    response.Servers[1].Id.Should().Be("192.168.0.20");
    response.Servers[1].DomainName.Should().Be("storage-b.example.com");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ListStorageServersResponseTests" -v minimal`
Expected: FAIL because the current response still uses local block-size resolution and duplicated fixed-field reads.

- [ ] **Step 3: Write the minimal implementation**

```csharp
using FastDFS.Client.Protocol.Decoding;
```

```csharp
protected override void DecodeBody(byte[]? body)
{
    if (body == null || body.Length == 0)
    {
        Servers = new List<StorageServerDetail>();
        return;
    }

    int serverInfoBlockSize = ProtocolBlockGuard.ResolveSupportedBlockSize(nameof(ListStorageServersResponse), body.Length, SupportedServerInfoBlockSizes);
    var responseBody = body;
    int serverCount = responseBody.Length / serverInfoBlockSize;
    Servers = new List<StorageServerDetail>(serverCount);

    for (int i = 0; i < serverCount; i++)
    {
        int offset = i * serverInfoBlockSize;
        Servers.Add(ParseServer(responseBody, offset, i));
    }
}
```

```csharp
private static StorageServerDetail ParseServer(byte[] body, int offset, int index)
{
    try
    {
        var joinTime = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 167));
        var lastHeartbeatTime = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 175));
        var lastSourceUpdate = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 375));
        var lastSyncUpdate = DateTimeOffset.FromUnixTimeSeconds(ByteConverter.ToInt64(body, offset + 383));

        var server = new StorageServerDetail
        {
            Status = (StorageServerStatus)body[offset],
            Id = ProtocolFieldReader.ReadFixedUtf8(body, offset + 1, 16),
            SourceIpAddress = ProtocolFieldReader.ReadFixedUtf8(body, offset + 17, 16),
            DomainName = ProtocolFieldReader.ReadFixedUtf8(body, offset + 33, 128),
            Version = ProtocolFieldReader.ReadFixedUtf8(body, offset + 161, 6),
            JoinTime = joinTime.UtcDateTime,
            LastHeartbeatTime = lastHeartbeatTime.UtcDateTime,
            TotalMB = ByteConverter.ToInt64(body, offset + 183),
            FreeMB = ByteConverter.ToInt64(body, offset + 191),
            UploadPriority = checked((int)ByteConverter.ToInt64(body, offset + 199)),
            StorePathCount = checked((int)ByteConverter.ToInt64(body, offset + 207)),
            SubdirCountPerPath = checked((int)ByteConverter.ToInt64(body, offset + 215)),
            CurrentWritePath = checked((int)ByteConverter.ToInt64(body, offset + 223)),
            StoragePort = checked((int)ByteConverter.ToInt64(body, offset + 231)),
            StorageHttpPort = checked((int)ByteConverter.ToInt64(body, offset + 239)),
            TotalUploadCount = ByteConverter.ToInt64(body, offset + 247),
            SuccessUploadCount = ByteConverter.ToInt64(body, offset + 255),
            TotalAppendCount = ByteConverter.ToInt64(body, offset + 263),
            SuccessAppendCount = ByteConverter.ToInt64(body, offset + 271),
            TotalModifyCount = ByteConverter.ToInt64(body, offset + 279),
            SuccessModifyCount = ByteConverter.ToInt64(body, offset + 287),
            TotalTruncateCount = ByteConverter.ToInt64(body, offset + 295),
            SuccessTruncateCount = ByteConverter.ToInt64(body, offset + 303),
            TotalSetMetadataCount = ByteConverter.ToInt64(body, offset + 311),
            SuccessSetMetadataCount = ByteConverter.ToInt64(body, offset + 319),
            TotalDeleteCount = ByteConverter.ToInt64(body, offset + 327),
            SuccessDeleteCount = ByteConverter.ToInt64(body, offset + 335),
            TotalDownloadCount = ByteConverter.ToInt64(body, offset + 343),
            SuccessDownloadCount = ByteConverter.ToInt64(body, offset + 351),
            TotalGetMetadataCount = ByteConverter.ToInt64(body, offset + 359),
            SuccessGetMetadataCount = ByteConverter.ToInt64(body, offset + 367),
            LastSourceUpdate = lastSourceUpdate.UtcDateTime,
            LastSyncUpdate = lastSyncUpdate.UtcDateTime
        };

        server.IpAddress = server.Id;
        return server;
    }
    catch (Exception ex)
    {
        throw new FastDFSProtocolException(
            $"Failed to parse storage server detail block at index {index}.",
            ex);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ListStorageServersResponseTests" -v minimal`
Expected: PASS with both 592-byte and 600-byte compatibility coverage intact plus new malformed-length checks.

- [ ] **Step 5: Commit**

```bash
git add src/FastDFS.Client/Protocol/Responses/ListStorageServersResponse.cs tests/FastDFS.Client.Tests/Protocol/Responses/ListStorageServersResponseTests.cs
git commit -m "refactor(protocol): harden storage server list parsing"
```

### Task 8: Run Phase 2 Regression Sweep

**Files:**
- Modify: `tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj` (only if new folders need explicit include updates)
- Test: `tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj`

- [ ] **Step 1: Run the focused Phase 2 suite**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj --filter "FullyQualifiedName~ProtocolFieldReaderTests|FullyQualifiedName~ProtocolBlockGuardTests|FullyQualifiedName~StorageServerIdTests|FullyQualifiedName~ListStorageServersRequestTests|FullyQualifiedName~QueryStoreAllResponseTests|FullyQualifiedName~QueryFetchAllResponseTests|FullyQualifiedName~ListAllGroupsResponseTests|FullyQualifiedName~ListStorageServersResponseTests" -v minimal`
Expected: PASS with all selected management and batch protocol tests green.

- [ ] **Step 2: Run the full test project**

Run: `dotnet test tests/FastDFS.Client.Tests/FastDFS.Client.Tests.csproj -v minimal`
Expected: PASS for the entire unit test project.

- [ ] **Step 3: Review changed files for Phase 2 scope control**

Run: `git diff --stat 919f17d..HEAD`
Expected: Only Phase 2 protocol/domain/request/response/test files appear; no metadata-wide cleanup or DI semantic refactor yet.

- [ ] **Step 4: Commit the validation checkpoint**

```bash
git add src/FastDFS.Client tests/FastDFS.Client.Tests
git commit -m "test(protocol): validate phase 2 management sweep"
```

## Self-Review

### Spec Coverage

- Shared protocol field reading is covered in Task 1.
- Batch block alignment helpers are covered in Task 2.
- Management-path storage server identifier validation is covered in Task 3.
- `ListStorageServersRequest` hardening is covered in Task 4.
- `QueryStoreAllResponse` and `QueryFetchAllResponse` hardening is covered in Task 5.
- `ListAllGroupsResponse` hardening is covered in Task 6.
- `ListStorageServersResponse` dual-block-size hardening is covered in Task 7.
- Regression and scope control for the whole phase are covered in Task 8.
- Metadata and DI semantic cleanup are intentionally excluded from this plan.

### Placeholder Scan

- No task contains deferred placeholders or incomplete implementation markers.
- Each code-changing step includes concrete code to add or adapt.
- Each validation step includes an exact command and expected outcome.

### Type Consistency

- `ProtocolFieldReader.ReadFixedUtf8(...)` is introduced before any response task depends on it.
- `ProtocolBlockGuard.EnsureExactBlockMultiple(...)` and `ResolveSupportedBlockSize(...)` are introduced before any response task depends on them.
- `StorageServerId.Create(...)` is introduced before `ListStorageServersRequest` depends on it.
- `ListStorageServersResponse` continues to rely on the existing `SupportedServerInfoBlockSizes` field rather than inventing a new block-size constant name later in the plan.
