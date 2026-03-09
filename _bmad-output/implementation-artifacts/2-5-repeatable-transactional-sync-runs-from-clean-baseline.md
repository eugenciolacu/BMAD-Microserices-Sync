# Story 2.5: Repeatable Transactional Sync Runs from Clean Baseline

Status: done

## Story

As a developer,
I want to repeat the core and edge-case scenarios multiple times from a clean baseline with transactional, batched sync,
so that I can build confidence in reliability and repeatability for larger effective datasets.

## Acceptance Criteria

1. **Given** the "clean & seed" baseline reset is available  
   **When** I run the documented cycle (reset → generate → transactional batched push → transactional batched pull → verify) multiple times  
   **Then** each run completes successfully without unexplained failures.

2. **Given** I execute the standard scenario (5 clients × 10 measurements) 10 times from a clean baseline with batching enabled  
   **When** I compare datasets after each run  
   **Then** ServerService and all ClientService instances converge to identical datasets every time.

3. **Given** I run the documented edge-case variant repeatedly from a clean baseline with batching enabled  
   **When** I inspect results over several runs  
   **Then** I observe consistent, correct convergence behavior that matches the documented expectations.

## Tasks / Subtasks

- [x] **Task 1: Add "Full Scenario Cycle (Repeatable Runs)" documentation to ServerService home page** (AC: #1, #2, #3)
  - [x] 1.1 Open `ServerService/Views/Home/Index.cshtml`.
  - [x] 1.2 Add a "Full Scenario Cycle — Repeatable Runs" section **after** the existing "Edge-Case Scenario: 3-Client Reduced-Volume Batching" section. Use a `<section>` element with an `<h3>` heading. Use a numbered `<ol>` list for the steps.
  - [x] 1.3 The section must document the complete cycle for the **standard scenario (5 clients × 10 measurements)**:
    1. Click "Reset" on this ServerService page. *(Clears all Measurements; re-seeds reference data, 0 measurements.)*
    2. On each of the 5 ClientService UIs: click "Reset Client DB". *(Clears SQLite database. No ref data yet.)*
    3. On each of the 5 ClientService UIs: click "Pull Reference Data" in the Administration section. *(Re-seeds Buildings, Rooms, Surfaces, Cells, Users from ServerService.)*
    4. On each of the 5 ClientService UIs: click "Generate Measurements". *(Creates 10 new measurements per client with fresh GUIDs — no conflict with previous runs.)*
    5. On each of the 5 ClientService UIs: click "Push Measurements". *(Sends all local measurements to ServerService in batches of 5, inside a single transaction.)*
    6. Verify ServerService Measurements grid shows 50 records (5 clients × 10 each).
    7. On each of the 5 ClientService UIs: click "Pull Measurements". *(Pull all 50 measurements from ServerService into each client in batches of 5, inside a single transaction.)*
    8. On each of the 5 ClientService UIs: click "Verify Convergence". *(Each client should report 50 local measurements matching server's 50.)*
    9. To repeat: return to Step 1. Every new Generate creates unique GUIDs so no ID conflicts occur across runs.
  - [x] 1.4 Below the ordered list, add a `<p>` note explaining why repeats are clean: *"Each run generates measurements with new GUIDs (Guid.NewGuid()), so repeated cycles from a fresh reset never produce duplicate IDs. The transactional push and pull ensure all-or-nothing semantics — partial failures leave no residue."*
  - [x] 1.5 Add an **edge-case repeat note** (short paragraph or `<ul>` bullet): for the 3-client edge-case variant (from the section above), the same cycle applies but only reset/generate/push/pull clients 1–3.

- [x] **Task 2: Write integration tests proving multi-run repeatability** (AC: #1, #2)
  - [x] 2.1 Create `MicroservicesSync.Tests/Measurements/RepeatableSyncRunTests.cs`.
  - [x] 2.2 Use the same test infrastructure patterns established in `MeasurementPullTests.cs` and `MeasurementGenerationTests.cs`:
    - Declare a local `internal sealed class TestableServerDbContextForRepeatable` using the same `IsRowVersion()` → `ValueGeneratedNever()` override pattern.
    - Use in-memory SQLite (`SqliteConnection` kept open) for both server and client `DbContext` instances.
    - Implement `IDisposable` to close connections and dispose contexts.
    - Seed reference data via a `SeedReferenceDataAsync()` helper that creates User → Building → Room → Surface → Cell hierarchy, capturing `_seedUserId` and `_seedCellId` as instance fields.
    - Use `MsOptions.Create(new SyncOptions { MeasurementsPerClient = 3, BatchSize = 2 })` for compact, fast tests.
  - [x] 2.3 Test `FullCycle_RunTwice_BothRunsConverge`:
    - **Setup**: Two runs simulated sequentially, resetting between them.
    - **Run 1**:
      - Generate 3 measurements via `MeasurementGenerationService.GenerateAsync()`.
      - Push via `MeasurementSyncService.PushAsync()` (uses a `StubHttpClient` / `MockHttpMessageHandler` that accepts push and returns success — see NOTE T1 for mock setup pattern from `MeasurementPushTests.cs`).
      - Simulate server receiving and storing the 3 measurements in `TestableServerDbContextForRepeatable`.
      - Pull via `MeasurementSyncService.PullAsync()` (mock returns all 3 server measurements as JSON).
      - Assert local count = 3, server count = 3.
    - **Reset** (simulating `ResetClientAsync` + `ResetServerAsync`): call `DatabaseResetter.ResetClientAsync(_clientDb)` and `DatabaseResetter.ResetServerAsync(_serverDb)`, then re-seed reference data.
    - **Run 2**:
      - Generate 3 new measurements.
      - Assert the new measurement GUIDs are **different** from Run 1's GUIDs.
      - Push and pull (same mock pattern).
      - Assert local count = 3, server count = 3.
    - **Final assert**: No exceptions thrown across both runs; counts converge to 3 after each run.
  - [x] 2.4 Test `NewMeasurementsAfterReset_HaveFreshGuids`:
    - Generate 3 measurements; store their IDs.
    - Call `DatabaseResetter.ResetClientAsync(_clientDb)`, then re-seed reference data.
    - Generate 3 more measurements; store their IDs.
    - Assert that the two ID sets have **zero intersection** (`!set1.Overlaps(set2)`).
    - This proves GUIDs do not conflict across repeated runs, guaranteeing no duplicate-key errors on push/pull.
  - [x] 2.5 Test `PullAfterReset_AppliesAllNewMeasurements`:
    - Simulate a client that has already pulled measurements (pre-populated `_clientDb.Measurements` with 3 entries).
    - Call `DatabaseResetter.ResetClientAsync(_clientDb)`, then re-seed reference data.
    - Assert `_clientDb.Measurements` count = 0 after reset.
    - Pull 3 new measurements (from mock server response with new GUIDs).
    - Assert `_clientDb.Measurements` count = 3. No `DbUpdateException` thrown.
    - This validates that the TOCTOU guard in `PullAsync` (existing-ID check) does not incorrectly block fresh pulls after a reset.
  - [x] 2.6 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [x] 2.7 Run `dotnet test` — all existing 44 tests pass; new tests pass (target: 47 total, 44 + 3 new).

## Dev Notes

### Architecture Constraints (MUST follow)

- **No new sync logic — Story 2.5 adds zero changes to push/pull services**: `PushAsync` and `PullAsync` in `MeasurementSyncService` are complete and correct from Stories 2.2 and 2.3. Do NOT touch them.
- **No new API endpoints**: Story 2.5 does not add any new HTTP endpoints on either service. All required endpoints (reset, generate, push, pull, count, verify-convergence) already exist from preceding stories.
- **No changes to domain entities**: No schema changes. No new columns. GUIDs are already used for all primary keys.
- **No changes to docker-compose.yml**: The standard topology (5 clients × `SyncOptions__MeasurementsPerClient=10`, `SyncOptions__BatchSize=5`) is already configured and correct for the standard scenario.
- **Story 2.5 deliverables are**: (1) a documentation section on ServerService home page, and (2) repeatability tests.
- **Why Story 2.5 works without code changes**: Fresh GUIDs are generated in every `GenerateAsync()` call via `Guid.NewGuid()`. After `ResetClientAsync`, the SQLite `Measurements` table is empty. After `ResetServerAsync`, the SQL Server `Measurements` table is empty. Therefore, every new cycle starts with zero overlapping IDs, and the TOCTOU duplicate-ID filter in `PullAsync` has nothing to filter. Repeat cycles are intrinsically safe.

### Why Repeatability Is Guaranteed by Existing Architecture

The following facts — drawn from prior stories' implementations — explain why no new sync logic is needed:

1. **GUIDs prevent cross-run ID collisions**: `MeasurementGenerationService.GenerateAsync()` calls `Guid.NewGuid()` for each measurement's `Id`. After a database reset, the next generate cycle creates entirely new GUIDs. Push and the TOCTOU ID-existence check in `PullAsync` are never triggered by old IDs (they're gone).

2. **`SyncedAt` tracking is reset-clean**: `PushAsync` sets `SyncedAt = UtcNow` on pushed measurements. After `ResetClientAsync`, all measurements (and their `SyncedAt` values) are deleted. The next `GenerateAsync()` creates un-synced measurements (`SyncedAt = null`), which `PushAsync` correctly picks up as pending.

3. **Server receives each push fresh**: `ResetServerAsync` deletes all `Measurements` rows and re-seeds reference data (no measurements). Each new push from any client after a reset adds records that ServerService has genuinely never seen.

4. **`pull-reference-data` idempotency guard is reset-friendly**: `POST api/v1/admin/pull-reference-data` on ClientService checks `if (!await db.Cells.AnyAsync())` before pulling. After `ResetClientAsync` (which clears Cells), this guard correctly allows a fresh reference-data pull. This avoids double-seeding if a restart triggers the auto-seed at the same time — but in practice, after a manual reset, the developer triggers pull-reference-data manually before generating measurements.

5. **Transactional guarantees hold across repeated runs**: Both push (server-side transaction per push operation) and pull (client-side transaction per pull operation) use `BeginTransactionAsync()`. There is no state carried over from one run to the next that could cause a transaction to behave differently.

### Note on `pull-reference-data` After Reset (Important for Developer)

After `POST api/v1/admin/reset` on ClientService, the database is **completely empty** (no Users, Buildings, Rooms, Surfaces, Cells, Measurements). The developer MUST call `POST api/v1/admin/pull-reference-data` (or click "Pull Reference Data" in the UI) before generating measurements — otherwise `GenerateAsync` will fail because it needs a valid `CellId` and `UserId` to create `Measurement` records.

The `pull-reference-data` endpoint has an idempotency guard (`Cells` table non-empty → returns 400). This guard is intentional to prevent accidental double-seeding if services restart mid-scenario. After a manual client reset, Cells are always empty, so the guard never blocks a legitimate re-seed.

**Implication for tests**: When simulating a repeat cycle in tests, after calling `DatabaseResetter.ResetClientAsync(_clientDb)`, always call the seed helper again to restore `_seedUserId` and `_seedCellId` so subsequent `GenerateAsync()` calls have valid foreign keys.

### Mock Pattern for Push/Pull in Tests (`MockHttpMessageHandler`)

The `MeasurementCountTests.cs` file establishes a `MockHttpMessageHandler` inner class pattern (no Moq/NSubstitute needed). Use the same approach:

```csharp
private sealed class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Response);
}
```

For `PullAsync`, the mock must return a `MeasurementPullResponse` JSON payload. The type lives in a DTO assembled in `MeasurementSyncService`; inspect how `MeasurementPullTests.cs` serializes `MeasurementPullResponse` for the exact JSON shape.

For `PushAsync`, the mock must return a `MeasurementPushResult`-compatible JSON (the endpoint returns HTTP 200 with `{ count, message }`). Look at `MeasurementPushTests.cs` for the exact mock response shape.

### `StubHttpClientFactory` Pattern

`MeasurementPullTests.cs` and `MeasurementPushTests.cs` both use a local `StubHttpClientFactory` inner class that implements `IHttpClientFactory` and returns a pre-configured `HttpClient`:

```csharp
private sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public StubHttpClientFactory(HttpClient client) => _client = client;
    public HttpClient CreateClient(string name) => _client;
}
```

Declare this locally in `RepeatableSyncRunTests.cs` — do NOT import from other test files (same assembly, different named types).

### `DatabaseResetter` API (from `Reset/DatabaseResetterTests.cs` patterns)

```csharp
// Async static methods in Sync.Infrastructure.Data (or similar namespace):
await DatabaseResetter.ResetServerAsync(serverDbContext, cancellationToken);
await DatabaseResetter.ResetClientAsync(clientDbContext, cancellationToken);
```

`ResetServerAsync`: deletes all data in FK-safe order, then re-seeds 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells. **No measurements seeded.**
`ResetClientAsync`: deletes all data (all tables). **No re-seed.** Developer must call pull-reference-data afterward.

Confirm the exact namespace and assembly by checking `DatabaseResetterTests.cs` — import whatever namespace it uses.

### Project Structure Notes

**Files to create:**
- `MicroservicesSync.Tests/Measurements/RepeatableSyncRunTests.cs` — 3 repeatability tests

**Files to modify:**
- `ServerService/Views/Home/Index.cshtml` — add "Full Scenario Cycle — Repeatable Runs" documentation section

**Files to NOT modify:**
- `ClientService/Services/MeasurementSyncService.cs` — push/pull logic is complete; do NOT touch
- `ServerService/Controllers/SyncMeasurementsController.cs` — push/pull/count endpoints are complete
- `ClientService/Controllers/AdminController.cs` — reset endpoint is complete
- `ServerService/Controllers/AdminController.cs` — reset endpoint is complete
- `ClientService/Program.cs` — no DI changes needed
- `docker-compose.yml` — no topology changes
- Any domain entity in `Sync.Domain`
- Any existing test file

### References

- Reset behavior: [Source: `MicroservicesSync/ClientService/Controllers/AdminController.cs`] and [Source: `MicroservicesSync/ServerService/Controllers/AdminController.cs`]
- `DatabaseResetter` API: [Source: `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs`]
- PushAsync / PullAsync: [Source: `MicroservicesSync/ClientService/Services/MeasurementSyncService.cs`] and Story 2.2 / Story 2.3 notes
- `SyncOptions` (MeasurementsPerClient, BatchSize): [Source: `MicroservicesSync/Sync.Application/Options/SyncOptions.cs`]
- Previous story learnings (TOCTOU, StubHttpClientFactory, MockHttpMessageHandler): [Source: `_bmad-output/implementation-artifacts/2-4-edge-case-multi-client-scenario-variant-with-batching.md`]
- Edge-case scenario UI (already on ServerService home page): [Source: `_bmad-output/implementation-artifacts/2-4-edge-case-multi-client-scenario-variant-with-batching.md`]
- Test patterns (TestableServerDbContext, SQLite in-memory, seed helpers): [Source: `MicroservicesSync/MicroservicesSync.Tests/Measurements/MeasurementPullTests.cs`] and [Source: `MicroservicesSync/MicroservicesSync.Tests/Measurements/MeasurementGenerationTests.cs`]
- Epics: Story 2.5 acceptance criteria [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.5`]
- Architecture: sync transactions and batching, reliability/repeatability (NFR2, NFR3) [Source: `_bmad-output/planning-artifacts/architecture.md`]
- Project Context: capability rules, reset/seed behavior [Source: `_bmad-output/project-context.md`]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

- Run 1 of implementation: `FullCycle_RunTwice_BothRunsConverge` failed with `FOREIGN KEY constraint failed` (SQLite Error 19) because server-side measurement inserts used random client `_seedUserId`/`_seedCellId` GUIDs instead of the stable GUIDs seeded by `DatabaseSeeder` (used by `ResetServerAsync`). Fixed by introducing `ServerSeedUserId`/`ServerSeedCellId` static fields with the `DatabaseSeeder` stable values and calling `ResetServerAsync` at the start of Run 1 to ensure server reference data is present before inserting measurements.

### Completion Notes List

- Task 1 complete: "Full Scenario Cycle — Repeatable Runs" section added to `ServerService/Views/Home/Index.cshtml` after the Edge-Case Scenario section, with `<section>` / `<h3>` / `<ol>` / explanation paragraph / edge-case repeat note.
- Task 2 complete: `MicroservicesSync.Tests/Measurements/RepeatableSyncRunTests.cs` created with 3 tests: `FullCycle_RunTwice_BothRunsConverge`, `NewMeasurementsAfterReset_HaveFreshGuids`, `PullAfterReset_AppliesAllNewMeasurements`.
- Build: 0 errors, 0 warnings.
- Tests: 47 total — 44 existing pass, 3 new pass.

### File List
- `MicroservicesSync/ServerService/Views/Home/Index.cshtml` — added "Full Scenario Cycle — Repeatable Runs" documentation section
- `MicroservicesSync/MicroservicesSync.Tests/Measurements/RepeatableSyncRunTests.cs` — created with 3 repeatability tests (`FullCycle_RunTwice_BothRunsConverge`, `NewMeasurementsAfterReset_HaveFreshGuids`, `PullAfterReset_AppliesAllNewMeasurements`)
