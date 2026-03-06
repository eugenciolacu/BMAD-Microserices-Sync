# Story 2.2: Transactional, Batched Server-Side Measurement Push

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want ClientService instances to push their local measurements to ServerService in batches inside a single transaction,
so that either all pushed measurements for that client are applied or none are, even for larger datasets.

## Acceptance Criteria

1. **Given** multiple ClientService instances have generated local measurements  
   **When** I trigger the documented push operation from a client  
   **Then** the client sends its measurements to ServerService in configurable-size batches (for example, 5 records per batch — `SyncOptions:BatchSize`) while ServerService processes all batches for that push inside a **single database transaction**.

2. **Given** a push operation completes successfully  
   **When** I inspect the ServerService database and Measurements grid  
   **Then** all measurements sent by that client for that push are present exactly once, with no missing or partially applied batches. The client's local `SyncedAt` field is updated to the UTC timestamp of the push.

3. **Given** an error occurs while applying any batch within a push (for example, a validation or DB error)  
   **When** the push operation ends  
   **Then** the transaction is rolled back and **none** of that client's new measurements from that push are persisted on ServerService, and the failure is visible in logs or client-facing feedback.

## Tasks / Subtasks

- [x] **Task 1: Define push DTOs on ServerService** (AC: #1, #2)
  - [x] 1.1 Create `ServerService/Models/Sync/MeasurementPushDto.cs` with three classes:
    ```csharp
    namespace ServerService.Models.Sync;

    public class MeasurementPushRequest
    {
        public List<MeasurementPushItemDto> Measurements { get; set; } = new();
    }

    public class MeasurementPushItemDto
    {
        public Guid Id { get; set; }
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }

    public class MeasurementPushResponse
    {
        public int Pushed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    ```
  - [x] 1.2 **STOP HERE** — Do NOT add `SyncedAt` to `MeasurementPushItemDto`. The server always sets
    `SyncedAt = null` when storing pushed measurements; the client sets its own `SyncedAt` after
    a successful push response.

- [x] **Task 2: Create `SyncMeasurementsController` on ServerService** (AC: #1, #2, #3)
  - [x] 2.1 Create `ServerService/Controllers/SyncMeasurementsController.cs`:
    - `[ApiController]`, `[Route("api/v1/sync")]`, extends `ControllerBase`
    - Constructor injects: `ServerDbContext db`, `IOptions<SyncOptions> syncOptions`,
      `ILogger<SyncMeasurementsController> logger`
    - Private fields: `_db`, `_batchSize = syncOptions.Value.BatchSize`, `_logger`
  - [x] 2.2 Implement `[HttpPost("measurements/push")]` endpoint:
    ```csharp
    [HttpPost("measurements/push")]
    public async Task<IActionResult> Push(
        [FromBody] MeasurementPushRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Measurements.Count == 0)
            return BadRequest(new { message = "No measurements provided." });

        // Partition into in-memory batches; ALL processed in a single transaction.
        var batches = request.Measurements
            .Select((m, i) => new { m, i })
            .GroupBy(x => x.i / _batchSize)
            .Select(g => g.Select(x => x.m).ToList())
            .ToList();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in batches)
            {
                var entities = batch.Select(dto => new Measurement
                {
                    Id = dto.Id,
                    Value = dto.Value,
                    RecordedAt = dto.RecordedAt,
                    SyncedAt = null,
                    UserId = dto.UserId,
                    CellId = dto.CellId
                }).ToList();

                await _db.Measurements.AddRangeAsync(entities, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "SyncMeasurementsController: pushed {Count} measurements in {Batches} batch(es).",
                request.Measurements.Count, batches.Count);

            return Ok(new MeasurementPushResponse
            {
                Pushed = request.Measurements.Count,
                Message = $"Pushed {request.Measurements.Count} measurements in {batches.Count} batch(es)."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SyncMeasurementsController: push failed — transaction rolled back.");
            return StatusCode(500, new { message = "Push failed. Transaction rolled back.", error = ex.Message });
        }
    }
    ```
  - [x] 2.3 Add the necessary `using` statements:
    ```csharp
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using ServerService.Models.Sync;
    using Sync.Application.Options;
    using Sync.Domain.Entities;
    using Sync.Infrastructure.Data;
    ```
  - [x] 2.4 **CRITICAL**: EF Core manual transaction pattern with SQL Server — `BeginTransactionAsync`
    returns an `IDbContextTransaction` that must be disposed. Use `await using var transaction = ...`
    to guarantee disposal even on exception. The `try/catch` block should call `CommitAsync` on
    success — rollback happens automatically when the transaction is disposed without committing
    (but explicit `RollbackAsync` in catch is acceptable and should be included for clarity/logging).

- [x] **Task 3: Define push DTOs on ClientService** (AC: #1)
  - [x] 3.1 Create `ClientService/Models/Sync/MeasurementPushDto.cs`:
    ```csharp
    namespace ClientService.Models.Sync;

    /// <summary>
    /// Mirrors the POST /api/v1/sync/measurements/push request body for ServerService.
    /// Used only by MeasurementSyncService to push local measurements upstream.
    /// </summary>
    internal sealed class ClientMeasurementPushRequest
    {
        public List<ClientMeasurementPushItemDto> Measurements { get; set; } = new();
    }

    internal sealed class ClientMeasurementPushItemDto
    {
        public Guid Id { get; set; }
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }
    ```
  - [x] 3.2 Use `internal sealed` — consistent with existing `ClientService/Models/Sync/SyncReferenceDataDto.cs` pattern.
  - [x] 3.3 Name them `ClientMeasurementPush*` (not `MeasurementPush*`) to avoid namespace collision
    if server and client are ever referenced in the same context. The `Client` prefix is a deliberate
    disambiguation, NOT a sign of wrong architecture.

- [x] **Task 4: Create `MeasurementSyncService` on ClientService** (AC: #1, #2, #3)
  - [x] 4.1 Create `ClientService/Services/MeasurementSyncService.cs`:
    - `public class MeasurementSyncService`
    - Constructor injects: `ClientDbContext db`, `IHttpClientFactory httpClientFactory`,
      `IOptions<SyncOptions> syncOptions`, `ILogger<MeasurementSyncService> logger`
    - Private fields: `_db`, `_httpClientFactory`, `_batchSize = syncOptions.Value.BatchSize`, `_logger`
  - [x] 4.2 Add `// NOTE (M2.2):` comment at the top of the class:
    ```csharp
    // NOTE (M2.2): MeasurementSyncService injects ClientDbContext directly (not via IMeasurementRepository)
    // and IHttpClientFactory to call ServerService. This is a deliberate, acknowledged deviation from strict
    // Application-layer separation, consistent with MeasurementGenerationService (NOTE M2.1) and
    // AdminController (NOTE M4). Full repository abstraction is out of scope for Story 2.2.
    ```
  - [x] 4.3 Implement `public async Task<MeasurementPushResult> PushAsync(CancellationToken cancellationToken = default)`:
    ```csharp
    public async Task<MeasurementPushResult> PushAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: Load all unsynced measurements from local SQLite.
        var pending = await _db.Measurements
            .AsNoTracking()
            .Where(m => m.SyncedAt == null)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            _logger.LogInformation("MeasurementSyncService: no pending measurements to push.");
            return new MeasurementPushResult(0, "No pending measurements to push.");
        }

        // Step 2: Build push request DTO.
        var pushRequest = new ClientMeasurementPushRequest
        {
            Measurements = pending
                .Select(m => new ClientMeasurementPushItemDto
                {
                    Id = m.Id,
                    Value = m.Value,
                    RecordedAt = m.RecordedAt,
                    UserId = m.UserId,
                    CellId = m.CellId
                })
                .ToList()
        };

        // Step 3: POST to ServerService.
        var http = _httpClientFactory.CreateClient("ServerService");
        var response = await http.PostAsJsonAsync(
            "api/v1/sync/measurements/push", pushRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "MeasurementSyncService: ServerService rejected push (HTTP {Status}): {Body}",
                (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"ServerService rejected push (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        // Step 4: Mark pushed measurements as synced in local SQLite.
        // Uses ExecuteUpdateAsync (bulk SQL) to avoid EF change-tracking / ConcurrencyStamp complexity.
        var syncedAt = DateTime.UtcNow;
        var pushedIds = pending.Select(m => m.Id).ToList();

        await _db.Measurements
            .Where(m => pushedIds.Contains(m.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.SyncedAt, syncedAt),
                cancellationToken);

        _logger.LogInformation(
            "MeasurementSyncService: successfully pushed {Count} measurements; SyncedAt updated.",
            pending.Count);

        return new MeasurementPushResult(pending.Count,
            $"Pushed {pending.Count} measurements to ServerService.");
    }
    ```
  - [x] 4.4 Add the `MeasurementPushResult` record at the bottom of the file (same file, below class):
    ```csharp
    public record MeasurementPushResult(int Count, string Message);
    ```
  - [x] 4.5 Add the necessary `using` statements:
    ```csharp
    using ClientService.Models.Sync;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;
    using Sync.Application.Options;
    using Sync.Infrastructure.Data;
    using System.Net.Http.Json;
    ```

- [x] **Task 5: Add push endpoint to `MeasurementsController`** (AC: #1, #2, #3)
  - [x] 5.1 Open `ClientService/Controllers/MeasurementsController.cs` — currently has only
    `[HttpPost("generate")]`. Add `MeasurementSyncService` injection alongside `MeasurementGenerationService`.
    Update the constructor to accept both services.
  - [x] 5.2 Add `[HttpPost("push")]` action:
    ```csharp
    [HttpPost("push")]
    public async Task<IActionResult> Push(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _syncService.PushAsync(cancellationToken);
            _logger.LogInformation("ClientService: push completed — {Count} measurements.", result.Count);
            return Ok(new { message = result.Message, pushed = result.Count });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ClientService: push failed — server rejected.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: push failed unexpectedly.");
            return StatusCode(500, new { message = "Push failed.", error = ex.Message });
        }
    }
    ```
  - [x] 5.3 **STOP HERE** — Do NOT add `GetPaged`, `GetById`, `Create`, `Update`, `Delete`, or
    `Pull` endpoints. jqGrid CRUD is Story 3.2 scope. Pull is Story 2.3 scope.

- [x] **Task 6: Register `MeasurementSyncService` in `ClientService/Program.cs`** (AC: #1)
  - [x] 6.1 Add after the existing `builder.Services.AddScoped<MeasurementGenerationService>();` line:
    ```csharp
    builder.Services.AddScoped<MeasurementSyncService>();
    ```
  - [x] 6.2 Do NOT restructure `Program.cs`. Add only this one line in the DI registration block.
  - [x] 6.3 `System.Net.Http.Json` namespace is already available via `ClientService.csproj` transitive
    reference — no new NuGet package is required.

- [x] **Task 7: Add "Push Measurements" UI to ClientService home page** (AC: #1, #2)
  - [x] 7.1 Open `ClientService/Views/Home/Index.cshtml` — study the existing "Generate Measurements"
    block (added in Story 2.1) to match the visual and script style exactly.
  - [x] 7.2 Add a "Push Measurements" section **after** the "Generate Measurements" `<div>` block
    (still within the "Sync Scenario Controls" section):
    ```html
    <div class="mb-3">
        <h4>Push Measurements to ServerService</h4>
        <p>Pushes all unsynced local measurements (where <code>SyncedAt == null</code>) to ServerService
           in batched, transactional form. The server applies all batches in a single transaction.</p>
        <p><em>Prerequisites: measurements must be generated on this instance first.</em></p>
        <button id="btnPush" onclick="pushMeasurements()" class="btn btn-success">Push Measurements</button>
        <div id="pushResult" class="mt-2"></div>
    </div>
    ```
  - [x] 7.3 Add JavaScript function (within the existing `<script>` block, alongside `generateMeasurements()`):
    ```javascript
    function pushMeasurements() {
        document.getElementById('btnPush').disabled = true;
        fetch('/api/v1/measurements/push', { method: 'POST' })
            .then(async r => {
                const data = await r.json();
                if (!r.ok) throw new Error(data.message || 'Request failed (' + r.status + ')');
                return data;
            })
            .then(data => {
                const el = document.createElement('div');
                el.className = 'alert alert-success mt-2';
                el.textContent = data.message;
                const box = document.getElementById('pushResult');
                box.innerHTML = '';
                box.appendChild(el);
            })
            .catch(err => {
                const el = document.createElement('div');
                el.className = 'alert alert-danger mt-2';
                el.textContent = 'Push failed: ' + (err.message || err);
                const box = document.getElementById('pushResult');
                box.innerHTML = '';
                box.appendChild(el);
            })
            .finally(() => { document.getElementById('btnPush').disabled = false; });
    }
    ```

- [x] **Task 8: Write tests** (AC: #1, #2, #3)
  - [x] 8.1 Create `MicroservicesSync.Tests/Measurements/MeasurementPushTests.cs`
    - **Server-side tests**: use `TestableServerDbContext` (already defined in
      `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs`) — copy the pattern (SqliteConnection +
      TestableServerDbContext constructor) exactly. The `TestableServerDbContext` class is private to
      that file (`internal sealed`), so you must either duplicate it in this new test file OR extract
      it to a shared internal helper class. **Recommended**: duplicate a local `TestableServerDbContext`
      in the new file (same pattern, same override, just locally declared). No test infrastructure
      changes required.
    - Use `NullLogger<SyncMeasurementsController>.Instance` for the logger.
    - Create `IOptions<SyncOptions>` via: `Options.Create(new SyncOptions { BatchSize = 3 })`
      (use `BatchSize = 3` in tests to make batching behavior observable with smaller datasets).
    - Seed required FK parents (Users, Buildings, Rooms, Surfaces, Cells) before inserting Measurements.
  - [x] 8.2 Implement the following test cases:
    - `Push_ValidMeasurements_AllStoredOnServer`: push 6 measurements with `BatchSize=3` → verify all 6 in `_serverDb.Measurements`
    - `Push_EmptyRequest_ReturnsBadRequestResult`: pass empty `MeasurementPushRequest` → `IActionResult` is `BadRequestObjectResult`
    - `Push_BatchedCorrectly_AllMeasurementsPresent`: push 12 measurements with `BatchSize=3` → verify all 12 present and distinct IDs
    - `Push_NoPendingMeasurements_ReturnsZeroCount`: call `MeasurementSyncService.PushAsync()` when local DB has no measurements with `SyncedAt == null` → result `Count == 0`
  - [x] 8.3 **Note on AC#3 (rollback) testing**: A unit test that verifies transaction rollback on DB error
    is difficult without SQL Server. Rely on the Docker smoke test for this scenario. You MAY add a
    test that verifies the controller returns 500 if `ServerDbContext.SaveChanges` throws (by using a
    mock or by crafting an invalid FK), but this is **optional** for Story 2.2.
  - [x] 8.4 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [x] 8.5 Run `dotnet test` — all 26 existing tests pass; new push tests pass (30 total: 26 + 4).
  - [x] 8.6 Manual Docker smoke test: `docker-compose up --build`, then:
    1. Trigger `POST /api/v1/measurements/generate` on each client
    2. Inspect each client Measurements grid — verify all have `SyncedAt = null`
    3. Trigger `POST /api/v1/measurements/push` on client1
    4. Inspect ServerService Measurements grid — verify all client1 measurements present
    5. Inspect client1 local DB — verify `SyncedAt` is now non-null for pushed records

## Dev Notes

### Architecture Constraints (MUST follow)

- **`SyncMeasurementsController` on ServerService uses route `[Route("api/v1/sync")]`** with method
  path `[HttpPost("measurements/push")]` → full route: `POST /api/v1/sync/measurements/push`.
  This is the canonical sync endpoint prefix from the architecture, consistent with the existing
  `SyncReferenceDataController` which ALSO uses `[Route("api/v1/sync")]`. Both controllers share
  the same base route — this is intentional and ASP.NET Core handles it correctly.

- **Single transaction pattern for ServerService**: Use `await using var transaction = await _db.Database.BeginTransactionAsync(...)`.
  Call `CommitAsync` inside try block. The transaction disposes automatically on exception (rollback).
  Do NOT use `TransactionScope` or `EF Core SaveChanges` auto-transaction for this — the explicit
  transaction is required so that multiple `SaveChangesAsync` calls within the loop all participate
  in the same transaction scope.

- **In-memory batching on ServerService, NOT multiple HTTP requests**: The client sends ALL unsynced
  measurements in a SINGLE HTTP POST request body. The server internally chunks the received list
  into `BatchSize` groups in memory and processes them inside ONE transaction. This is the "in-memory
  batches" pattern from the architecture. Do NOT design the client to send one HTTP request per batch.

- **`ExecuteUpdateAsync` for updating `SyncedAt` on ClientService**: After a successful push, update
  the local SQLite measurements using `ExecuteUpdateAsync`. This bypasses EF change tracking and the
  `ConcurrencyStamp` shadow property concern entirely, avoiding `DbUpdateConcurrencyException`.
  Do NOT load-modify-save via EF tracking for this bulk update. This was flagged as "Story 2.2+ concern"
  in Story 2.1 notes — `ExecuteUpdateAsync` is the correct resolution.

- **`MeasurementSyncService` lives in `ClientService/Services/`**, NOT in `Sync.Application`.
  It requires `ClientDbContext` (Infrastructure) AND `IHttpClientFactory` (ASP.NET Core concern),
  both of which are wrong dependencies for a shared Application service. Document with `// NOTE (M2.2):`.

- **`IHttpClientFactory` named client `"ServerService"` is already registered** in `ClientService/Program.cs`:
  ```csharp
  builder.Services.AddHttpClient("ServerService", client => { ... });
  ```
  Do NOT add a new `AddHttpClient` call. Simply inject `IHttpClientFactory` and call
  `_httpClientFactory.CreateClient("ServerService")`. The base address and timeout are already configured.

- **`System.Net.Http.Json` is already available** in `ClientService.csproj` transitively via the
  `Microsoft.AspNetCore.App` framework reference. The `PostAsJsonAsync` extension method is in this namespace.
  No new NuGet package required for `PostAsJsonAsync`.

- **`SyncOptions.BatchSize` is already defined** in `Sync.Application/Options/SyncOptions.cs`
  with a default of `5`. The configuration key `SyncOptions__BatchSize` (env var) or
  `SyncOptions:BatchSize` (appsettings) overrides the default. Both services already register
  `IOptions<SyncOptions>` in their respective `Program.cs` files. Do NOT redefine this option.

- **`SyncedAt` semantics**: After a successful push, update `SyncedAt = DateTime.UtcNow` on the
  CLIENT side for pushed measurements. The SERVER stores `SyncedAt = null` for pushed measurements
  (it doesn't track whether a given measurement came from a push). This asymmetry is intentional —
  `SyncedAt` on the client means "this record was successfully pushed to the server."

- **Do NOT add `SyncedAt` to `MeasurementPushItemDto`**. The server sets `SyncedAt = null` on insert.
  The client updates its own `SyncedAt` after a successful response. The field is NOT needed in the
  push payload.

- **Parameterized queries only.** All DB operations use EF Core LINQ — no raw SQL string interpolation.
  `AddRangeAsync`, `ExecuteUpdateAsync`, `Where` with lambda predicates. This satisfies NFR5.

- **RowVersion on SQL Server (ServerService)**: When inserting `Measurement` entities, leave `RowVersion`
  at its default `Array.Empty<byte>()`. The SQL Server `rowversion` / `timestamp` type is
  `ValueGeneratedOnAddOrUpdate` — the DB auto-generates it on insert. EF Core knows this because
  `ServerDbContext.OnModelCreating` calls `.IsRowVersion()`. Do NOT set `RowVersion` manually.

- **No auth on sync endpoints**: Per architecture ADR, all sync endpoints are open within the Docker
  network. Do NOT add `[Authorize]`, API keys, or other auth to `SyncMeasurementsController`.

### Key Implementation Files

| File | Action | Notes |
|---|---|---|
| `ServerService/Models/Sync/MeasurementPushDto.cs` | **CREATE** | `MeasurementPushRequest`, `MeasurementPushItemDto`, `MeasurementPushResponse` |
| `ServerService/Controllers/SyncMeasurementsController.cs` | **CREATE** | `POST /api/v1/sync/measurements/push`, batch + transaction logic |
| `ClientService/Models/Sync/MeasurementPushDto.cs` | **CREATE** | `ClientMeasurementPushRequest`, `ClientMeasurementPushItemDto` (internal sealed) |
| `ClientService/Services/MeasurementSyncService.cs` | **CREATE** | Push to server, mark `SyncedAt` via `ExecuteUpdateAsync` |
| `ClientService/Controllers/MeasurementsController.cs` | **MODIFY** | Add `MeasurementSyncService` injection, `POST push` action |
| `ClientService/Views/Home/Index.cshtml` | **MODIFY** | Add "Push Measurements" button + JS fetch function |
| `ClientService/Program.cs` | **MODIFY** | +1 `AddScoped<MeasurementSyncService>()` line |
| `MicroservicesSync.Tests/Measurements/MeasurementPushTests.cs` | **CREATE** | 4 test cases (server + client unit tests) |

### Reference Files (read before writing)

- [ServerService/Controllers/SyncReferenceDataController.cs](../../../MicroservicesSync/ServerService/Controllers/SyncReferenceDataController.cs)
  — Existing sync controller on ServerService. Match route convention `[Route("api/v1/sync")]` and
  the direct `ServerDbContext` injection pattern.
- [ClientService/Services/MeasurementGenerationService.cs](../../../MicroservicesSync/ClientService/Services/MeasurementGenerationService.cs)
  — Model for `MeasurementSyncService`. Same direct `ClientDbContext` with `// NOTE` comment.
  Match the constructor pattern, field naming, and logger usage exactly.
- [ClientService/Controllers/MeasurementsController.cs](../../../MicroservicesSync/ClientService/Controllers/MeasurementsController.cs)
  — Existing controller with `[HttpPost("generate")]`. Add `push` action here. Match error handling
  shape (try/catch with `InvalidOperationException → BadRequest`, `Exception → 500`).
- [ClientService/Controllers/AdminController.cs](../../../MicroservicesSync/ClientService/Controllers/AdminController.cs)
  — Reference for: `// NOTE (M4)` pattern, `IHttpClientFactory.CreateClient("ServerService")` usage,
  and `PostAsJsonAsync` call shape. The `pull-reference-data` action shows how to POST JSON to server.
- [ClientService/Program.cs](../../../MicroservicesSync/ClientService/Program.cs)
  — DI registration style. Add `AddScoped<MeasurementSyncService>()` immediately after the existing
  `AddScoped<MeasurementGenerationService>()` line to keep related registrations together.
- [ClientService/Models/Sync/SyncReferenceDataDto.cs](../../../MicroservicesSync/ClientService/Models/Sync/SyncReferenceDataDto.cs)
  — Pattern for `internal sealed` DTOs in `ClientService/Models/Sync/`. Match this style for
  `ClientMeasurementPushRequest` and `ClientMeasurementPushItemDto`.
- [Sync.Application/Options/SyncOptions.cs](../../../MicroservicesSync/Sync.Application/Options/SyncOptions.cs)
  — Shows `BatchSize = 5` default. Do NOT redefine this property.
- [MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs](../../../MicroservicesSync/MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs)
  — Copy the `TestableServerDbContext` subclass pattern (SQLite-compatible ServerDbContext override)
  for server-side push tests. The `SqliteConnection` + `EnsureCreated()` setup pattern applies here.
- [MicroservicesSync.Tests/Measurements/MeasurementGenerationTests.cs](../../../MicroservicesSync/MicroservicesSync.Tests/Measurements/MeasurementGenerationTests.cs)
  — Copy the `ClientDbContext` in-memory SQLite setup pattern + `SeedCellsAsync` helper for any
  client-side tests. Note the full FK chain: User → Building → Room → Surface → Cell → Measurement.

### Previous Story Learnings (from Story 2.1)

**Patterns to follow:**
- `// NOTE (M2.1):` deviation pattern → use `// NOTE (M2.2):` for `MeasurementSyncService` with
  the same justification structure.
- `Options.Create(new SyncOptions { ... })` for test IOptions injection (no DI needed in tests).
- `NullLogger<T>.Instance` for logger in tests (already transitively available, no new NuGet).
- `IDisposable` + `SqliteConnection` (kept open) + `DbContext` + `EnsureCreated()` test setup pattern.
- `Random.Shared.NextDouble()` — not applicable here, but if generating in tests: always use this.
- `DateTime.UtcNow` — ALWAYS, never `DateTime.Now`. Required for `SyncedAt` update.
- `AsNoTracking()` for read-only queries — follow this pattern in `MeasurementSyncService.PushAsync`.

**Code patterns established:**
- `ClientService/Services/` — service files for ClientService-specific concerns with direct DbContext injection.
- `ClientService/Models/Sync/` — DTOs for server communication (internal sealed).
- `ClientService/Options/` — options POCOs specific to this service.
- Test FK chain (in-memory SQLite): User + Building + Room + Surface + Cell must all be seeded BEFORE
  inserting Measurements. In-memory SQLite DOES enforce FK constraints when `EnsureCreated()` is used
  AND when the Measurement table has FK columns. Seed the full chain.

**Problems encountered in Story 2.1:**
- `ClientDbContext.OnModelCreating` uses `HasDefaultValue(0L)` for `ConcurrencyStamp` shadow property
  on INSERT — do NOT set manually. ✓
- `RowVersion` is `Ignore`d on `ClientDbContext` — do NOT configure or set. ✓
- `SyncedAt = null` on generation — now in Story 2.2, after push succeeds, it becomes `DateTime.UtcNow`.

### Key Data Facts

**`SyncOptions.BatchSize`** configuration cascade (highest priority wins):
1. Environment variable `SyncOptions__BatchSize` (set in docker-compose.yml — currently `5`)
2. `appsettings.Development.json` — `SyncOptions:BatchSize`
3. `appsettings.json` — `SyncOptions:BatchSize` (default `5`)

**Client containers + ports** (for Docker smoke test reference):
| Container | Port | `ClientIdentity__UserId` |
|---|---|---|
| clientservice_user1 | 5001 | `00000000-0000-0000-0000-000000000001` |
| clientservice_user2 | 5002 | `00000000-0000-0000-0000-000000000002` |
| clientservice_user3 | 5003 | `00000000-0000-0000-0000-000000000003` |
| clientservice_user4 | 5004 | `00000000-0000-0000-0000-000000000004` |
| clientservice_user5 | 5005 | `00000000-0000-0000-0000-000000000005` |

**ServerService port:** `8080` (or `localhost:8080` outside Docker).

### Seed helper for server-side tests

```csharp
private async Task SeedServerReferenceDataAsync(TestableServerDbContext db)
{
    var userId   = new Guid("00000000-0000-0000-0000-000000000001");
    var buildingId = Guid.NewGuid();
    var roomId   = Guid.NewGuid();
    var surfaceId  = Guid.NewGuid();
    var cellId   = Guid.NewGuid();

    db.Users.Add(new User { Id = userId, Username = "test-user", Email = "test@test.com" });
    db.Buildings.Add(new Building { Id = buildingId, Identifier = "test-building" });
    db.Rooms.Add(new Room { Id = roomId, Identifier = "test-room", BuildingId = buildingId });
    db.Surfaces.Add(new Surface { Id = surfaceId, Identifier = "test-surface", RoomId = roomId });
    db.Cells.Add(new Cell { Id = cellId, Identifier = "test-cell-0", SurfaceId = surfaceId });

    await db.SaveChangesAsync();

    // Store these IDs for use in test MeasurementPushItemDtos
    _seedUserId = userId;
    _seedCellId = cellId;
}
```
You will need `_seedUserId` and `_seedCellId` as private fields in the test class (set during seeding)
to construct valid `MeasurementPushItemDto` instances in tests.

### `PostAsJsonAsync` vs `PostAsync` reference

Use `PostAsJsonAsync` from `System.Net.Http.Json`:
```csharp
var http = _httpClientFactory.CreateClient("ServerService");
var response = await http.PostAsJsonAsync(
    "api/v1/sync/measurements/push", pushRequest, cancellationToken);
```
This serializes `pushRequest` to JSON and sets `Content-Type: application/json`.
The server's `[ApiController]` + `[FromBody]` will deserialize it automatically.

### Transaction rollback verified by

For AC#3 (rollback on error), manual verification in Docker:
```
docker-compose up --build
# Generate measurements on client1
curl -X POST http://localhost:5001/api/v1/measurements/generate
# Manually inject an invalid measurement ID that will cause a PK conflict
# (e.g., push twice without reset)
curl -X POST http://localhost:5001/api/v1/measurements/push
curl -X POST http://localhost:5001/api/v1/measurements/push  # second push → duplicate PK error
# Verify ServerService shows no partial data after the failed second push
```
A second push with all the same GUIDs will fail at the DB level (PK violation), and the entire
transaction rolls back — confirming AC#3 behavior.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

No blockers encountered. All tasks implemented cleanly on first pass.

### Completion Notes List

- **Task 1**: Created `ServerService/Models/Sync/MeasurementPushDto.cs` with `MeasurementPushRequest`, `MeasurementPushItemDto`, and `MeasurementPushResponse`. `SyncedAt` intentionally excluded from DTO per spec.
- **Task 2**: Created `SyncMeasurementsController` at `POST /api/v1/sync/measurements/push`. Uses `await using var transaction = await _db.Database.BeginTransactionAsync(...)` with explicit `RollbackAsync` in catch for clarity. In-memory batching (GroupBy) with single transaction wrapping all batches.
- **Task 3**: Created `ClientService/Models/Sync/MeasurementPushDto.cs` with `internal sealed` classes named `ClientMeasurementPush*` to avoid namespace collision. Consistent with `SyncReferenceDataDto.cs` pattern.
- **Task 4**: Created `MeasurementSyncService` with `// NOTE (M2.2)` deviation comment. Uses `AsNoTracking()` for read query, `PostAsJsonAsync` for HTTP push, and `ExecuteUpdateAsync` for bulk `SyncedAt` update (bypasses EF change-tracking/ConcurrencyStamp concern).
- **Task 5**: Updated `MeasurementsController` — added `MeasurementSyncService` injection alongside `MeasurementGenerationService` and added `[HttpPost("push")]` action with same error-handling shape as `generate`.
- **Task 6**: Added `builder.Services.AddScoped<MeasurementSyncService>()` immediately after `AddScoped<MeasurementGenerationService>()` in `ClientService/Program.cs`. No other changes to `Program.cs`.
- **Task 7**: Added "Push Measurements" `<div>` block after "Generate Measurements" in `Index.cshtml`. Added `pushMeasurements()` JavaScript function in the existing `<script>` block matching the established fetch pattern.
- **Task 8**: Added `ServerService` project reference to test project. Created `MeasurementPushTests.cs` with local `TestableServerDbContext` (same SQLite-compatible override pattern). 4 tests: server push (6 measurements, BatchSize=3), empty request → BadRequest, 12 measurements all stored with distinct IDs, and no-pending early-exit returning Count=0. `dotnet build`: 0 errors, 0 warnings. `dotnet test`: 30/30 passing (26 existing + 4 new).

### File List

- `ServerService/Models/Sync/MeasurementPushDto.cs` — CREATED
- `ServerService/Controllers/SyncMeasurementsController.cs` — CREATED
- `ClientService/Models/Sync/MeasurementPushDto.cs` — CREATED
- `ClientService/Services/MeasurementSyncService.cs` — CREATED
- `ClientService/Controllers/MeasurementsController.cs` — MODIFIED
- `ClientService/Views/Home/Index.cshtml` — MODIFIED
- `ClientService/Program.cs` — MODIFIED
- `MicroservicesSync.Tests/Measurements/MeasurementPushTests.cs` — CREATED (5 tests: 4 original + 1 rollback/AC#3)
- `MicroservicesSync.Tests/MicroservicesSync.Tests.csproj` — MODIFIED (added ServerService project reference)

### Change Log

- 2026-03-06: Implemented Story 2.2 — transactional batched server-side measurement push. Added `POST /api/v1/sync/measurements/push` on ServerService (single DB transaction, in-memory batching). Added `MeasurementSyncService` on ClientService (push unsynced measurements, `ExecuteUpdateAsync` for bulk `SyncedAt`). Added `POST /api/v1/measurements/push` on ClientService controller + "Push Measurements" UI button. 4 new unit tests; 30/30 passing.
- 2026-03-06: Code review by GitHub Copilot (Claude Sonnet 4.6). Fixed 4 medium issues: (M1) `RollbackAsync` called with a cancellable token in catch block — changed to `CancellationToken.None`; (M2) dead `_batchSize` field and `IOptions<SyncOptions>` constructor parameter removed from `MeasurementSyncService` (client-side batching is not performed); (M3) `BatchSize <= 0` guard added to `SyncMeasurementsController` constructor; (M4) AC#3 rollback test added (`Push_DuplicatePush_RollsBackAndReturns500`). 4 low issues noted (ex.Message exposure, null Measurements guard, async keyword in generateMeasurements(), ID fidelity assertion). Total: 31/31 tests passing. Story → done.
