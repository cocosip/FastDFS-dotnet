# FastDFS Protocol and Domain Hardening Design

## Summary

This design defines a long-term refactor for the FastDFS .NET client to eliminate protocol drift, inconsistent API semantics, and fragmented validation logic. The refactor keeps the public client surface mostly stable while moving protocol rules, identifier rules, and metadata rules into dedicated internal components with clear ownership.

The immediate goal is not only to fix the currently identified issues, but to make future protocol commands safer to add and easier to test. The end state is a codebase where fixed-width FastDFS fields are validated in one place, malformed responses fail deterministically, and public APIs no longer depend on duplicated helper logic.

## Goals

- Centralize FastDFS protocol encoding and decoding rules.
- Centralize domain validation for file identifiers, group names, extensions, and metadata.
- Preserve existing public APIs where practical while correcting unsafe behavior.
- Replace silent truncation and partial parsing with explicit failures.
- Expand tests so protocol compatibility and API semantics are both regression protected.

## Non-Goals

- Rewriting the transport layer or connection pool architecture.
- Replacing the current `FastDFSClient`, `TrackerClient`, or `StorageClient` responsibilities.
- Redesigning the dependency injection surface for new end-user concepts.
- Introducing breaking public API changes unless a correctness fix requires one.

## Problems Being Addressed

### 1. Protocol constraints are duplicated and inconsistent

Fixed-width FastDFS fields such as `groupName`, `storageServerId`, and extension fields are currently encoded by individual request classes. Some code paths truncate long values silently instead of rejecting them. This can produce valid packets with incorrect semantics, which is worse than a clear failure.

### 2. Domain rules are spread across helpers and entry points

Rules for file ID parsing, extension handling, and metadata validity are split across `FastDFSClient`, `FileIdHelper`, request classes, and `FastDFSMetadata`. The same conceptual input can succeed in one path and fail in another.

### 3. Response parsing is not strict enough

Some response types parse based on integer division instead of validating that the body length exactly matches the expected block structure. This allows truncated or malformed tracker responses to be treated as partially successful results.

### 4. DI client discovery semantics are unclear

The DI factory mixes configuration-backed discovery, runtime registration, and instantiated client tracking. As a result, `HasClient` and `GetClientNames` do not reflect the same truth source.

## Architecture

The refactor introduces three stable internal layers and keeps the existing orchestration layer thin:

- Protocol layer: owns FastDFS byte layout rules and response block validation.
- Domain layer: owns identifier parsing, extension semantics, and metadata validity.
- Application layer: `FastDFSClient`, `TrackerClient`, `StorageClient`, and factories coordinate operations and compose validated inputs and outputs.

The application layer should not perform low-level field-length decisions. Request and response types should not own ad hoc string truncation or body-shape inference. Instead, both rely on shared internal helpers with uniform error behavior.

## Component Design

### Protocol Encoding Module

Create a small internal encoding helper area under `src/FastDFS.Client/Protocol/Encoding/`.

Planned types:

- `ProtocolFieldWriter`
- `ProtocolFieldLengthGuard`

Responsibilities:

- Write fixed-width string fields with explicit UTF-8 byte-length validation.
- Normalize acceptable protocol representations before writing bytes.
- Reject `null`, out-of-range, or oversize values with precise argument exceptions.
- Remove repeated `Array.Copy(... Math.Min(...))` patterns from request classes.

This module becomes the single place that defines how FastDFS fixed-width fields are encoded.

### Protocol Decoding Module

Create a matching internal decoding helper area under `src/FastDFS.Client/Protocol/Decoding/`.

Planned types:

- `ProtocolFieldReader`
- `ProtocolBlockGuard`

Responsibilities:

- Validate that response body sizes match expected block sizes.
- Read fixed fields from response buffers using shared offset logic.
- Fail fast with `FastDFSProtocolException` when body shapes are malformed.
- Standardize error messages with block size, offset, and body length context.

This module becomes the single place that defines how fixed-layout tracker and storage responses are validated before parsing.

### Domain Identifier Module

Create an internal identifier area under `src/FastDFS.Client/Domain/Identifiers/`.

Planned types:

- `FastDFSFileId`
- `GroupName`
- `FileExtension`
- `StorageServerId`

Responsibilities:

- Parse and compose file IDs.
- Represent empty extension as a valid state and `null` extension as invalid input.
- Enforce protocol-relevant limits such as fixed-width group and server ID lengths.
- Remove duplicated identifier logic from `FastDFSClient` and request classes.

`FileIdHelper` remains temporarily as a compatibility shim and internally delegates to the new identifier logic where possible.

### Domain Metadata Module

Retain `FastDFSMetadata` as the main public metadata type, but tighten its invariants.

Responsibilities:

- Ensure all mutation paths validate keys and values.
- Prevent constructor-based and indexer-based bypass of separator validation.
- Keep encode/decode logic local to the metadata type so protocol helpers remain generic.

The target state is that a `FastDFSMetadata` instance is always internally valid.

### Application Layer Changes

The following classes remain the main orchestration layer:

- `FastDFSClient`
- `TrackerClient`
- `StorageClient`
- `FastDFSClientFactory`
- `FastDFSClientManager`

Their responsibilities are narrowed:

- Convert raw input into domain values early.
- Compose request and response objects.
- Propagate argument and protocol failures without embedding duplicate validation logic.
- Avoid custom fixed-field handling in high-level methods.

## Data Flow

The new normal request flow is:

1. `FastDFSClient` receives public input.
2. Public input is converted to validated domain values such as `FastDFSFileId`, `GroupName`, or `FileExtension`.
3. Request classes consume validated values and delegate fixed-width encoding to `ProtocolFieldWriter`.
4. `FastDFSConnection` sends and receives bytes and continues to own transport-level concerns.
5. Response classes validate body structure through `ProtocolBlockGuard` and read fields via `ProtocolFieldReader`.
6. Parsed results are returned as existing domain models such as `StorageServerInfo` and `GroupInfo`.

This keeps transport, protocol, and domain semantics separate.

## Behavioral Decisions

### Extension Semantics

- Empty extension is valid and means "upload without extension".
- `null` extension is invalid and should fail at the nearest public or domain boundary.
- `UploadFileAsync` should allow local files with no suffix by passing an empty extension through the stack.

### Fixed-Width Fields

- Oversize `groupName`, `storageServerId`, and similar fields are rejected explicitly.
- Silent truncation is never allowed for protocol-fixed fields.
- Limits are enforced based on encoded UTF-8 byte length, not just character count.

### Response Shape Validation

- Fixed-block responses must validate exact block alignment before parsing any item.
- Partial trailing bytes are always treated as protocol corruption.
- Malformed protocol responses must raise `FastDFSProtocolException`.

### Metadata Invariants

- Metadata keys and values cannot contain FastDFS record or field separators.
- All write paths must enforce the same validation rules.
- Invalid metadata must fail at object construction or mutation time, not later during request encoding.

### DI Client Discovery

The DI implementation should move toward an explicit registry-backed truth source.

Target behavior:

- Configuration registration records known names.
- Runtime registration records known names.
- Instantiated clients are a state derived from registration, not a separate discovery source.

This prevents `HasClient` and `GetClientNames` from diverging semantically.

## Migration Plan

The refactor is intentionally staged to avoid a large all-at-once rewrite.

### Phase 1: Core Upload and Fetch Path

Scope:

- Upload request types
- Download request types
- Tracker fetch/store query requests and responses
- `FastDFSClient` upload entry points

Objectives:

- Allow empty extensions.
- Introduce protocol field writers for the most frequently used request paths.
- Replace silent truncation with explicit rejection on core paths.

### Phase 2: Management and Batch Responses

Scope:

- `ListAllGroupsResponse`
- `ListStorageServersRequest`
- `ListStorageServersResponse`
- `QueryStoreAllResponse`
- `QueryFetchAllResponse`

Objectives:

- Introduce shared fixed-block response validation.
- Standardize body-shape errors for management operations.

### Phase 3: Domain Cleanup and Compatibility Convergence

Scope:

- `FastDFSMetadata`
- `FileIdHelper`
- DI factory registration and discovery behavior

Objectives:

- Ensure one set of domain invariants.
- Turn old helpers into thin compatibility wrappers.
- Clarify client registration semantics.

## File-Level Impact

### New Files

Planned additions:

- `src/FastDFS.Client/Protocol/Encoding/ProtocolFieldWriter.cs`
- `src/FastDFS.Client/Protocol/Encoding/ProtocolFieldLengthGuard.cs`
- `src/FastDFS.Client/Protocol/Decoding/ProtocolFieldReader.cs`
- `src/FastDFS.Client/Protocol/Decoding/ProtocolBlockGuard.cs`
- `src/FastDFS.Client/Domain/Identifiers/FastDFSFileId.cs`
- `src/FastDFS.Client/Domain/Identifiers/GroupName.cs`
- `src/FastDFS.Client/Domain/Identifiers/FileExtension.cs`
- `src/FastDFS.Client/Domain/Identifiers/StorageServerId.cs`

### Existing Files to Refactor First

Primary early targets:

- `src/FastDFS.Client/FastDFSClient.cs`
- `src/FastDFS.Client/Utilities/FileIdHelper.cs`
- `src/FastDFS.Client/FastDFSMetadata.cs`
- `src/FastDFS.Client/Protocol/Requests/UploadFileRequest.cs`
- `src/FastDFS.Client/Protocol/Requests/UploadAppenderFileRequest.cs`
- `src/FastDFS.Client/Protocol/Requests/DownloadFileRequest.cs`
- `src/FastDFS.Client/Protocol/Requests/QueryFetchRequest.cs`
- `src/FastDFS.Client/Protocol/Requests/ListStorageServersRequest.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryStoreResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryFetchResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryStoreAllResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryFetchAllResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/ListAllGroupsResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/ListStorageServersResponse.cs`
- `src/FastDFS.Client.DependencyInjection/FastDFSClientFactory.cs`

## Error Handling Strategy

### Argument Errors

Use `ArgumentException` or `ArgumentOutOfRangeException` when:

- Public callers supply invalid domain input.
- Fixed-width fields exceed protocol limits.
- Metadata keys or values contain forbidden separators.

These should fail before any packet is sent.

### Protocol Errors

Use `FastDFSProtocolException` when:

- Response body lengths do not match required block structure.
- Response parsing overruns expected offsets.
- Servers return malformed but transport-complete data.

These should surface with enough detail to diagnose malformed server output or parser mismatch.

### Network Errors

Keep existing transport behavior in `FastDFSConnection` and related classes. This design does not change the network exception model beyond ensuring protocol validation happens at clearer boundaries.

## Testing Strategy

### 1. Domain Tests

Add focused unit tests for:

- `FastDFSFileId`
- `FileExtension`
- `GroupName`
- `StorageServerId`
- `FastDFSMetadata`

Purpose:

- Validate semantics independently of sockets and protocol packet assembly.
- Ensure only one rule set exists for identifiers and metadata.

### 2. Protocol Golden Tests

Add byte-level request and response tests for:

- Empty extension encoding
- Oversize fixed-field rejection
- Exact fixed-block parsing
- Misaligned response body failure

Purpose:

- Lock protocol correctness to concrete byte expectations.
- Catch regressions when adding or modifying command support.

### 3. Public API Behavior Tests

Expand `FastDFSClientTests` to verify:

- Uploading a local file without an extension is allowed.
- Invalid oversize group names fail explicitly.
- Metadata invalidity is surfaced before request execution.

Purpose:

- Ensure end-user-observable behavior remains coherent after internal refactors.

### 4. Future Integration Coverage

Longer term, add Docker-backed FastDFS integration coverage for core upload, download, query, and management operations. This is not required for the first refactor wave, but it is the correct end-state for validating real protocol compatibility.

## Risks and Mitigations

### Risk: Hidden compatibility assumptions in request classes

Mitigation:

- Migrate high-traffic request types first.
- Preserve public signatures while changing validation behavior intentionally.
- Back changes with protocol golden tests.

### Risk: Over-refactoring too many areas at once

Mitigation:

- Keep transport and pooling unchanged.
- Stage work by protocol path, then management paths, then DI cleanup.
- Defer helper removal until replacements are stable.

### Risk: Breaking callers that relied on silent truncation

Mitigation:

- Treat explicit failure as the correct compatibility break because silent truncation is unsafe.
- Document the behavior change in release notes or changelog when implementation is shipped.

## Success Criteria

The refactor is successful when all of the following are true:

- Uploads without file extensions work end to end.
- No fixed-width protocol field is silently truncated anywhere in the request pipeline.
- Fixed-block responses fail explicitly on malformed lengths.
- `FastDFSMetadata` cannot be constructed or mutated into an invalid state.
- Public APIs derive identifier semantics from shared domain logic instead of duplicate helper code.
- DI client discovery semantics are based on explicit registration state rather than incidental instantiation state.
- New and updated tests cover protocol bytes, domain rules, and public behavior for the migrated paths.

