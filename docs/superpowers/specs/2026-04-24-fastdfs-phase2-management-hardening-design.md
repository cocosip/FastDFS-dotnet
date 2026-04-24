# FastDFS Phase 2 Management and Batch Protocol Hardening Design

## Summary

This design defines Phase 2 of the FastDFS protocol and domain hardening effort. Phase 1 established shared protocol writing guards, core identifier types, and stricter upload and fetch validation on the hot path. Phase 2 extends that work to the management and batch tracker path so that fixed-width request fields, fixed-block response parsing, and protocol failure behavior are handled consistently across the SDK.

The goal of this phase is to close the management-path protocol gap without dragging in unrelated cleanup from metadata invariants or DI client discovery. By the end of Phase 2, tracker management requests and batch responses should use shared protocol helpers, reject oversize fixed-width fields explicitly, and fail deterministically when response bodies are malformed.

## Goals

- Introduce shared fixed-width field reading for tracker response parsing.
- Introduce a dedicated `StorageServerId` domain type for management requests.
- Harden `ListStorageServersRequest` so it uses shared fixed-width encoding rules.
- Harden batch and management tracker responses so malformed body sizes always fail with `FastDFSProtocolException`.
- Unify management-path protocol behavior so partially parsed results are never returned.
- Expand tests to cover both supported storage-server block sizes and malformed response edge cases.

## Non-Goals

- Reworking `FastDFSMetadata` invariants in this phase.
- Replacing `FileIdHelper` with full identifier delegation in this phase.
- Changing DI registration and discovery semantics in this phase.
- Refactoring transport, pooling, or failover behavior.
- Broadly rewriting `TrackerClient` public API shape or introducing new public request/response abstractions.

## Problems Being Addressed

### 1. Management requests still encode fixed-width fields ad hoc

`ListStorageServersRequest` currently writes `groupName` and optional `storageServerId` with inline byte copying and silent truncation. That leaves management requests out of sync with the Phase 1 upload and fetch path hardening.

### 2. Batch responses still infer structure from raw integer division

`QueryStoreAllResponse`, `QueryFetchAllResponse`, and `ListAllGroupsResponse` compute item counts directly from `body.Length / blockSize` without first enforcing minimum length and exact block alignment through a shared guard. This allows malformed tracker payloads to slip through as partial success or fail inconsistently.

### 3. Fixed-field parsing logic remains duplicated across management responses

Several responses still read fixed-width strings inline with repeated offset math and repeated trim behavior. This makes parser behavior drift-prone and makes future protocol fixes expensive because each response must be audited separately.

### 4. Management-path errors are not classified consistently

Some malformed payloads currently surface as `ArgumentException`, while other protocol failures use `FastDFSProtocolException`. This makes tracker protocol faults harder to distinguish from caller input faults.

## Scope

Phase 2 covers the management and batch tracker path only.

### In Scope

- `src/FastDFS.Client/Protocol/Requests/ListStorageServersRequest.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryStoreAllResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryFetchAllResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/ListAllGroupsResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/ListStorageServersResponse.cs`
- `src/FastDFS.Client/Tracker/TrackerClient.cs` only where needed to keep management-path protocol errors coherent
- Shared protocol helpers under `Protocol/Decoding`
- Shared identifier types under `Domain/Identifiers`
- Response and request tests for the above paths

### Explicitly Out of Scope

- `FastDFSMetadata`
- `FileIdHelper` compatibility cleanup
- DI factory/client registry cleanup
- Storage-side management protocol additions beyond current tracker response coverage

## Recommended Approach

Three implementation approaches were considered:

1. Patch the affected responses individually.
2. Build out the protocol-layer management path using shared reader and identifier infrastructure.
3. Broaden the phase to include wider application-layer and DI cleanup.

This design chooses approach 2. It keeps the phase focused on the management protocol path while still producing reusable infrastructure. It avoids the underbuilt result of a patch-only pass and avoids the scope explosion of pulling Phase 3 work into the current effort.

## Architecture

Phase 2 extends the layered structure introduced in Phase 1.

- Domain layer: owns fixed-width identifier validity for management-path inputs.
- Protocol encoding layer: owns request-side fixed-width writes.
- Protocol decoding layer: owns fixed-width reads and block-shape validation.
- Application layer: `TrackerClient` composes requests, dispatches them, and surfaces either parsed models or explicit protocol failures.

The key architectural rule is that management response types should no longer decide body validity by hand. They may describe supported block sizes and field layout, but shared helpers own the mechanics of block validation and fixed-width field extraction.

## Component Design

### Protocol Decoding Additions

Add `ProtocolFieldReader` under `src/FastDFS.Client/Protocol/Decoding/`.

Responsibilities:

- Read fixed-width UTF-8 strings from a byte array at a specified offset and length.
- Apply shared null-padding and trailing-null trimming rules.
- Centralize offset-based fixed-field reads so response parsers stop duplicating string extraction logic.

This helper stays intentionally small. It should not know about `StorageServerDetail` or `GroupInfo`; it only reads protocol fields.

Extend `ProtocolBlockGuard` so it can validate both minimum body size and exact block alignment for fixed-block responses.

Responsibilities:

- Validate minimum non-empty response size.
- Validate exact multiples for single-block-size responses.
- Validate membership in supported block size sets for responses like `ListStorageServersResponse`.
- Throw `FastDFSProtocolException` with concrete body-length and block-size context.

### Domain Identifier Additions

Add `StorageServerId` under `src/FastDFS.Client/Domain/Identifiers/`.

Responsibilities:

- Represent the optional management-path storage server identifier.
- Reject `null` when explicitly constructed, but allow callers to skip the optional field by not constructing it.
- Enforce the FastDFS fixed-width 16-byte limit using UTF-8 byte count, just like `GroupName`.

This mirrors the role `FileExtension` and `GroupName` already play for Phase 1.

### Request Refactor

Refactor `ListStorageServersRequest` so it:

- Converts `GroupName` through the `GroupName` value object.
- Converts optional `StorageServerId` through the new `StorageServerId` value object when supplied.
- Uses `ProtocolFieldWriter` for both fixed-width fields.
- Rejects oversize values instead of truncating.

The request keeps the existing public properties and command shape so callers do not need to change.

### Response Refactor

Refactor these response types:

- `QueryStoreAllResponse`
- `QueryFetchAllResponse`
- `ListAllGroupsResponse`
- `ListStorageServersResponse`

Common changes:

- Validate body shape through `ProtocolBlockGuard` before parsing the first item.
- Use `ProtocolFieldReader` for fixed-width strings.
- Treat malformed body lengths as protocol corruption, not argument errors.
- Continue returning empty collections for legal empty bodies where the protocol permits that state.

Specific behavior:

- `QueryStoreAllResponse` must require exact multiples of the 40-byte storage info block.
- `QueryFetchAllResponse` must require exact multiples of the 39-byte fetch block.
- `ListAllGroupsResponse` must require exact multiples of the 105-byte group info block.
- `ListStorageServersResponse` must continue to support both 592-byte and 600-byte server blocks, but it must reject any body length that is not a clean multiple of one supported size.

## Data Flow

### Request Path

`TrackerClient.ListStorageServersAsync(...)`
-> validate `groupName` as `GroupName`
-> validate optional `storageServerId` as `StorageServerId`
-> `ListStorageServersRequest` encodes fixed-width fields via `ProtocolFieldWriter`
-> bytes are sent over the existing tracker connection path

The important behavior change is that oversize management request fields fail before any packet is sent.

### Response Path

`FastDFSConnection` receives the tracker body
-> response type calls `ProtocolBlockGuard`
-> response type uses `ProtocolFieldReader` and `ByteConverter`
-> parsed items are mapped into `StorageServerInfo`, `GroupInfo`, or `StorageServerDetail`
-> `TrackerClient` returns the full parsed collection or surfaces `FastDFSProtocolException`

This means management-path parsing becomes "validate shape first, parse second" everywhere.

## Behavioral Decisions

### Fixed-Width Management Fields

- `groupName` is still required for `ListStorageServersRequest`.
- `storageServerId` remains optional.
- When `storageServerId` is supplied, oversize values are rejected explicitly.
- Silent truncation is never allowed.

### Empty vs Malformed Management Responses

- Legal empty bodies still map to empty collections for current management responses that already treat emptiness as "no results".
- Non-empty malformed bodies are never partially parsed.
- Partial trailing bytes always fail the whole response.

### Supported Block Sizes

- `ListStorageServersResponse` continues supporting both 592-byte and 600-byte tracker formats for compatibility.
- The parser must choose one supported block size for the entire body, never a mix.
- Unsupported body lengths always fail with `FastDFSProtocolException`.

### Error Classification

- Caller-supplied invalid request fields continue to fail as argument errors.
- Malformed tracker response bodies always fail as `FastDFSProtocolException`.
- Management-path protocol faults should therefore be distinguishable from caller misuse in logs and exception handling.

## File-Level Impact

### New Files

- `src/FastDFS.Client/Protocol/Decoding/ProtocolFieldReader.cs`
- `src/FastDFS.Client/Domain/Identifiers/StorageServerId.cs`
- `tests/FastDFS.Client.Tests/Protocol/Decoding/ProtocolFieldReaderTests.cs`
- `tests/FastDFS.Client.Tests/Domain/Identifiers/StorageServerIdTests.cs`
- `tests/FastDFS.Client.Tests/Protocol/Responses/QueryStoreAllResponseTests.cs`
- `tests/FastDFS.Client.Tests/Protocol/Responses/QueryFetchAllResponseTests.cs`
- `tests/FastDFS.Client.Tests/Protocol/Responses/ListAllGroupsResponseTests.cs`
- `tests/FastDFS.Client.Tests/Protocol/Requests/ListStorageServersRequestTests.cs`

### Existing Files to Modify

- `src/FastDFS.Client/Protocol/Decoding/ProtocolBlockGuard.cs`
- `src/FastDFS.Client/Protocol/Requests/ListStorageServersRequest.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryStoreAllResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/QueryFetchAllResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/ListAllGroupsResponse.cs`
- `src/FastDFS.Client/Protocol/Responses/ListStorageServersResponse.cs`
- `src/FastDFS.Client/Tracker/TrackerClient.cs` only if needed to standardize management-path protocol error behavior
- Existing response test files where compatible coverage already exists

## Error Handling Strategy

### Argument Errors

Use `ArgumentException` or `ArgumentOutOfRangeException` when:

- `ListStorageServersRequest.GroupName` is missing or oversize.
- `ListStorageServersRequest.StorageServerId` is oversize.
- A new identifier type receives invalid caller input.

These are request construction failures and should happen before any network I/O.

### Protocol Errors

Use `FastDFSProtocolException` when:

- A non-empty body is shorter than the minimum required block size.
- A body is not a clean multiple of the required block size.
- `ListStorageServersResponse` receives a body length that matches neither supported server block size.
- Fixed-field parsing cannot proceed because the body shape is invalid.

These are server-side or protocol-contract failures.

### Application-Layer Behavior

`TrackerClient` should continue returning parsed collections for valid responses and empty collections for protocol-legal empty bodies. It should not wrap or downgrade `FastDFSProtocolException` emitted by the response layer on malformed management bodies.

## Testing Strategy

### 1. Identifier Tests

Add focused tests for `StorageServerId`:

- valid value preserved
- oversize UTF-8 value rejected
- `null` rejected at creation boundary

Purpose:

- lock fixed-width management identifier semantics independently of request classes

### 2. Protocol Reader Tests

Add `ProtocolFieldReaderTests` covering:

- fixed-width ASCII read with null padding trim
- offset-based reads
- empty field reads

Purpose:

- ensure shared field reading stays byte-accurate before multiple responses depend on it

### 3. Request Tests

Add `ListStorageServersRequestTests` covering:

- group-only encoding
- group plus server-id encoding
- oversize `groupName` rejection
- oversize `storageServerId` rejection

Purpose:

- lock management request semantics to explicit byte behavior and explicit failure

### 4. Response Tests

Add or expand tests for:

- `QueryStoreAllResponse`
- `QueryFetchAllResponse`
- `ListAllGroupsResponse`
- `ListStorageServersResponse`

Coverage:

- valid single-block parse
- valid multi-block parse
- empty body handling where allowed
- too-short body failure
- misaligned body failure
- dual-size `ListStorageServersResponse` support for 592 and 600 byte blocks

Purpose:

- prevent partial parsing regressions and keep compatibility with known tracker block variants

### 5. Management-Path Behavior Tests

If existing tracker tests make this practical, add focused `TrackerClient` management-path tests that verify malformed management responses surface as `FastDFSProtocolException` without being translated into partial results.

## Risks and Mitigations

### Risk: Compatibility break for callers who relied on truncation

Mitigation:

- treat explicit failure as the correct fix because truncated management identifiers are unsafe
- cover the change with request tests and release notes if the branch ships publicly

### Risk: Overbuilding the decoding helper layer

Mitigation:

- keep `ProtocolFieldReader` small and field-oriented
- do not introduce a large generic parser framework
- stop at what the current management responses need

### Risk: Body-shape rules become inconsistent again

Mitigation:

- make `ProtocolBlockGuard` the only place that validates fixed-block alignment rules
- write tests that assert both success and exact failure modes per response type

### Risk: Phase 2 drifts into Phase 3 cleanup

Mitigation:

- keep metadata and DI cleanup explicitly out of scope
- only touch `TrackerClient` where needed for management-path error coherence

## Success Criteria

Phase 2 is successful when all of the following are true:

- `ListStorageServersRequest` no longer truncates fixed-width fields silently.
- `StorageServerId` exists and owns the management-path fixed-width identifier rule.
- `ProtocolFieldReader` is used by the migrated management and batch responses.
- `QueryStoreAllResponse`, `QueryFetchAllResponse`, `ListAllGroupsResponse`, and `ListStorageServersResponse` validate body shape before parsing items.
- Malformed non-empty management responses always fail with `FastDFSProtocolException`.
- `ListStorageServersResponse` still supports both known tracker block sizes.
- Tests cover request bytes, identifier rules, valid block parsing, and malformed body rejection for the migrated management path.
