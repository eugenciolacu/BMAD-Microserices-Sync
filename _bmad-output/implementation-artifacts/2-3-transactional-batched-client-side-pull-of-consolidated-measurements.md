# Story 2.3: Transactional, Batched Client-Side Pull of Consolidated Measurements

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want each ClientService instance to pull the consolidated measurement set from ServerService in batches inside a single transaction,
so that each client either fully converges to the server dataset or not at all for that pull.

## Acceptance Criteria

1. **Given** ServerService holds the consolidated measurements after successful pushes  
   **When** I trigger the documented pull operation on a client  
   **Then** the client retrieves measurements from ServerService in configurable-size batches (for example, 5 records per batch — `SyncOptions:BatchSize`) and applies all **new** batches for that pull within a **single local database transaction**.

2. **Given** a pull operation completes successfully  
   **When** I compare measurement counts and identifiers across ServerService and that ClientService  
   **Then** they match exactly, with no missing, extra, or duplicate records (verified by comparing GUIDs).

3. **Given** an error occurs while applying any batch during a pull  
   **When** the pull operation ends  
   **Then** the local transaction is rolled back and the client's Measurements table remains **unchanged** by that pull attempt.

## Tasks / Subtasks

- [x] **Task 1: Add pull DTOs to ServerService** (AC: #1)
  - [x] 1.1 Create `ServerService/Models/Sync/MeasurementPullDto.cs` with two classes:
    ```csharp
    namespace ServerService.Models.Sync;

    public class MeasurementPullItemDto
    {
        public Guid Id { get; set; }
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }

    public class MeasurementPullResponse
    {
        public List<MeasurementPullItemDto> Measurements { get; set; } = new();
        public int Total { get; set; }
    }
    ```
  - [x] 1.2 **Note**: No `SyncedAt` in the pull DTO — the server stores all measurements with
    `SyncedAt = null`. The client determines its own `SyncedAt` state locally. Do NOT add it.

- [x] **Task 2: Add GET pull endpoint to `SyncMeasurementsController` on ServerService** (AC: #1, #2)
  - [x] 2.1 Open `ServerService/Controllers/SyncMeasurementsController.cs` — currently has only
    `[HttpPost("measurements/push")]`. Add the pull endpoint to the same controller class.
  - [x] 2.2 Add `[HttpGet("measurements/pull")]` action:
    ```csharp
    [HttpGet("measurements/pull")]
    public async Task<IActionResult> Pull(CancellationToken cancellationToken)
    {
        var measurements = await _db.Measurements
            .AsNoTracking()
            .Select(m => new MeasurementPullItemDto
            {
                Id = m.Id,
                Value = m.Value,
                RecordedAt = m.RecordedAt,
                UserId = m.UserId,
                CellId = m.CellId
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "SyncMeasurementsController: pull requested — returning {Count} measurements.",
            measurements.Count);

        return Ok(new MeasurementPullResponse
        {
            Measurements = measurements,
            Total = measurements.Count
        });
    }
    ```
  - [x] 2.3 Add the `using` statements needed (most are already present from Task 1 of push):
    ```csharp
    using Microsoft.EntityFrameworkCore; // for ToListAsync / AsNoTracking
    ```
    The `ServerService.Models.Sync` namespace is already in scope from the push implementation's
    existing `using` block. Add `using Microsoft.EntityFrameworkCore;` only if not already present.
  - [x] 2.4 **CRITICAL**: ServerService returns ALL measurements — no pagination/filtering for MVP.
    Do NOT add `since` or `lastSync` query parameters. Story 2.3 is about convergence, not incremental sync.
  - [x] 2.5 **CRITICAL**: No transaction needed on the ServerService pull handler — it is a read-only
    query. Transactions are only required for write operations (push).

- [x] **Task 3: Add pull DTOs to ClientService** (AC: #1)
  - [x] 3.1 Create `ClientService/Models/Sync/MeasurementPullDto.cs`:
    ```csharp
    namespace ClientService.Models.Sync;

    /// <summary>
    /// Mirrors the GET /api/v1/sync/measurements/pull response body from ServerService.
    /// Used only by MeasurementSyncService to receive and apply consolidated measurements.
    /// </summary>
    internal sealed class ClientMeasurementPullResponse
    {
        public List<ClientMeasurementPullItemDto> Measurements { get; set; } = new();
        public int Total { get; set; }
    }

    internal sealed class ClientMeasurementPullItemDto
    {
        public Guid Id { get; set; }
        public decimal Value { get; set; }
        public DateTime RecordedAt { get; set; }
        public Guid UserId { get; set; }
        public Guid CellId { get; set; }
    }
    ```
  - [x] 3.2 Use `internal sealed` — consistent with existing `ClientService/Models/Sync/SyncReferenceDataDto.cs`
    and `ClientMeasurementPushRequest` / `ClientMeasurementPushItemDto` patterns.
  - [x] 3.3 Name them `ClientMeasurementPull*` (not `MeasurementPull*`) to avoid namespace collision
    with ServerService DTOs. The `Client` prefix is intentional disambiguation.

- [x] **Task 4: Modify `MeasurementSyncService` on ClientService** (AC: #1, #2, #3)
  - [x] 4.1 Open `ClientService/Services/MeasurementSyncService.cs`.
  - [x] 4.2 **Add `IOptions<SyncOptions>` to the constructor** — PullAsync needs `_batchSize`
    to partition the received measurements into in-memory batches for local transaction processing.
    Update constructor signature:
    ```csharp
    public MeasurementSyncService(
        ClientDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<SyncOptions> syncOptions,
        ILogger<MeasurementSyncService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _batchSize = syncOptions.Value.BatchSize;
        if (_batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(syncOptions),
                $"SyncOptions.BatchSize must be > 0 (was {_batchSize}).");
        _logger = logger;
    }
    ```
  - [x] 4.3 Add private field `_batchSize` to the class:
    ```csharp
    private readonly int _batchSize;
    ```
    Place it after `_httpClientFactory` field declaration, before `_logger`.
  - [x] 4.4 Add `using Microsoft.Extensions.Options;` and `using Sync.Application.Options;` if not
    already present. They are already present from `PushAsync` implementation — **do NOT add duplicates**.
  - [x] 4.5 Add `PullAsync()` method after `PushAsync()`:
    ```csharp
    public async Task<MeasurementPullResult> PullAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: GET consolidated measurement list from ServerService.
        var http = _httpClientFactory.CreateClient("ServerService");
        var response = await http.GetFromJsonAsync<ClientMeasurementPullResponse>(
            "api/v1/sync/measurements/pull", cancellationToken);

        if (response == null || response.Measurements.Count == 0)
        {
            _logger.LogInformation("MeasurementSyncService: pull returned 0 measurements from ServerService.");
            return new MeasurementPullResult(0, "No measurements available on ServerService.");
        }

        // Step 2: Identify which measurement IDs are NEW (not yet in local SQLite).
        var serverIds = response.Measurements.Select(m => m.Id).ToHashSet();
        var existingIds = (await _db.Measurements
                .AsNoTracking()
                .Where(m => serverIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var newMeasurements = response.Measurements
            .Where(dto => !existingIds.Contains(dto.Id))
            .ToList();

        if (newMeasurements.Count == 0)
        {
            _logger.LogInformation(
                "MeasurementSyncService: all {Count} server measurements already present locally.",
                response.Total);
            return new MeasurementPullResult(0, "All measurements already present locally — no changes needed.");
        }

        // Step 3: Partition into in-memory batches; ALL applied in a single SQLite transaction.
        var batches = newMeasurements
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
                    SyncedAt = null, // Pulled measurements: SyncedAt=null (not pushed BY this client)
                    UserId = dto.UserId,
                    CellId = dto.CellId
                }).ToList();

                await _db.Measurements.AddRangeAsync(entities, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "MeasurementSyncService: pulled and applied {Count} new measurements in {Batches} batch(es).",
                newMeasurements.Count, batches.Count);
            return new MeasurementPullResult(newMeasurements.Count,
                $"Pulled {newMeasurements.Count} new measurements from ServerService in {batches.Count} batch(es).");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogError(ex,
                "MeasurementSyncService: pull failed — local transaction rolled back.");
            throw new InvalidOperationException(
                $"Pull failed — local transaction rolled back: {ex.Message}", ex);
        }
    }
    ```
  - [x] 4.6 Add `MeasurementPullResult` record at the bottom of the file (alongside `MeasurementPushResult`):
    ```csharp
    public record MeasurementPullResult(int Count, string Message);
    ```
  - [x] 4.7 Add the following `using` statement (if not already present):
    ```csharp
    using Sync.Domain.Entities; // for Measurement entity
    ```
    Check the top of the file — `Sync.Domain.Entities` should already be available transitively.
    If not present, add it explicitly.

- [x] **Task 5: Add `[HttpPost("pull")]` action to `MeasurementsController` on ClientService** (AC: #1, #2, #3)
  - [x] 5.1 Open `ClientService/Controllers/MeasurementsController.cs` — currently has `generate` and `push`.
    Add the pull action using the same error-handling shape.
  - [x] 5.2 Add `[HttpPost("pull")]` action after the push action:
    ```csharp
    [HttpPost("pull")]
    public async Task<IActionResult> Pull(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _syncService.PullAsync(cancellationToken);
            _logger.LogInformation("ClientService: pull completed — {Count} new measurements.", result.Count);
            return Ok(new { message = result.Message, pulled = result.Count });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ClientService: pull failed — operation error.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: pull failed unexpectedly.");
            return StatusCode(500, new { message = "Pull failed.", error = ex.Message });
        }
    }
    ```
  - [x] 5.3 **STOP HERE** — Do NOT add `GetPaged`, `GetById`, `Create`, `Update`, or `Delete` endpoints.
    jqGrid CRUD for Measurements is Story 3.2 scope. Do not implement it here.
  - [x] 5.4 Note: The controller already has `_syncService` (injected `MeasurementSyncService`)
    and `_logger` — no constructor changes needed.

- [x] **Task 6: Register updated `MeasurementSyncService` constructor in DI** (AC: #1)
  - [x] 6.1 Open `ClientService/Program.cs`. The `IOptions<SyncOptions>` parameter added in Task 4
    is automatically satisfied by the existing `builder.Services.Configure<SyncOptions>(...)` line.
    **No changes to `Program.cs` are required.** ASP.NET Core DI resolves `IOptions<T>` for any
    configured section automatically when the scoped service is resolved.
  - [x] 6.2 Do NOT restructure or add any other lines to `Program.cs` for this story.

- [x] **Task 7: Add "Pull Measurements" UI to ClientService home page** (AC: #1, #2)
  - [x] 7.1 Open `ClientService/Views/Home/Index.cshtml` — study the existing "Push Measurements"
    block to match visual and script style exactly.
  - [x] 7.2 Add a "Pull Measurements" section **after** the "Push Measurements" `<div>` block
    (still within the "Sync Scenario Controls" section):
    ```html
    <div class="mb-3">
        <h4>Pull Consolidated Measurements from ServerService</h4>
        <p>Retrieves the full measurement set from ServerService and applies any new (missing) measurements
           to this client's local database in batched, transactional form. Either all new measurements
           are applied or none (on error).</p>
        <p><em>Prerequisites: at least one push must have been completed (by any client) before pulling.</em></p>
        <button id="btnPullMeasurements" onclick="pullMeasurements()" class="btn btn-info">Pull Measurements</button>
        <div id="pullMeasurementsResult" class="mt-2"></div>
    </div>
    ```
  - [x] 7.3 Add JavaScript function (within the existing `<script>` block, alongside
    `generateMeasurements()` and `pushMeasurements()`):
    ```javascript
    function pullMeasurements() {
        document.getElementById('btnPullMeasurements').disabled = true;
        fetch('/api/v1/measurements/pull', { method: 'POST' })
            .then(async r => {
                const data = await r.json();
                if (!r.ok) throw new Error(data.message || 'Request failed (' + r.status + ')');
                return data;
            })
            .then(data => {
                const el = document.createElement('div');
                el.className = 'alert alert-success mt-2';
                el.textContent = data.message;
                const box = document.getElementById('pullMeasurementsResult');
                box.innerHTML = '';
                box.appendChild(el);
            })
            .catch(err => {
                const el = document.createElement('div');
                el.className = 'alert alert-danger mt-2';
                el.textContent = 'Pull failed: ' + (err.message || err);
                const box = document.getElementById('pullMeasurementsResult');
                box.innerHTML = '';
                box.appendChild(el);
            })
            .finally(() => { document.getElementById('btnPullMeasurements').disabled = false; });
    }
    ```
  - [x] 7.4 **Button ID disambiguation**: Use `btnPullMeasurements` and `pullMeasurementsResult`
    (NOT `btnPull` / `pullResult`) — those IDs are already used by the "Pull Reference Data" button
    in the Administration section. Using the same IDs would cause DOM conflicts.

- [x] **Task 8: Write tests** (AC: #1, #2, #3)
  - [x] 8.1 Create `MicroservicesSync.Tests/Measurements/MeasurementPullTests.cs`.
    - **Server-side tests:** Use `TestableServerDbContext` — **do NOT import from MeasurementPushTests.cs**.
      Declare a local `TestableServerDbContext` in this file using the same pattern (SQLite + RowVersion
      `ValueGeneratedNever` override). Check the existing `MeasurementPushTests.cs` template exactly.
    - **Client-side tests:** Use `ClientDbContext` with in-memory SQLite (same pattern as
      `MeasurementGenerationTests.cs` and `MeasurementPushTests.cs`).
    - Use `NullLogger<*>.Instance` for all loggers.
    - Use `Options.Create(new SyncOptions { BatchSize = 3 })` for IOptions (observable batching).
    - Use `using MsOptions = Microsoft.Extensions.Options.Options;` alias (already in push tests).
  - [x] 8.2 Implement the following test cases:

    **Server-side tests (SyncMeasurementsController.Pull):**

    - `Pull_WithMeasurements_ReturnsAllMeasurements`:
      - Seed server reference data (User, Building, Room, Surface, Cell).
      - Insert 6 Measurement entities directly into `_serverDb`.
      - Call `controller.Pull(CancellationToken.None)`.
      - Assert: result is `OkObjectResult`; deserialize response, verify `Total == 6` and
        `Measurements.Count == 6`.

    - `Pull_EmptyDatabase_ReturnsEmptyResponse`:
      - Do NOT seed any measurements on the server.
      - Call `controller.Pull(CancellationToken.None)`.
      - Assert: result is `OkObjectResult`; `Total == 0` and `Measurements.Count == 0`.

    **Client-side tests (MeasurementSyncService.PullAsync):**

    - `PullAsync_NewMeasurements_AllInsertedInLocalDb`:
      - Seed client reference data (User, Building, Room, Surface, Cell).
      - Mock `IHttpClientFactory` to return an `HttpClient` backed by a handler that returns a
        `ClientMeasurementPullResponse` with 12 `ClientMeasurementPullItemDto` items (distinct GUIDs).
      - Create `MeasurementSyncService` with `BatchSize = 3`.
      - Call `PullAsync()`.
      - Assert: `result.Count == 12`; `_clientDb.Measurements.Count() == 12`; all 12 GUIDs present;
        no duplicates.

    - `PullAsync_ExistingMeasurements_AreSkippedNotDuplicated`:
      - Seed client reference data.
      - Pre-insert 5 Measurement entities in `_clientDb` (simulate own pushed measurements).
      - Mock `IHttpClientFactory` to return those 5 existing GUIDs PLUS 5 new GUIDs (10 total).
      - Call `PullAsync()`.
      - Assert: `result.Count == 5` (only the 5 new ones inserted);
        `_clientDb.Measurements.Count() == 10` (5 pre-existing + 5 new); no duplicates.

    - `PullAsync_AllAlreadyPresent_ReturnsZeroCount`:
      - Seed client reference data.
      - Pre-insert 5 Measurement entities in `_clientDb`.
      - Mock `IHttpClientFactory` to return exactly those 5 same GUIDs.
      - Call `PullAsync()`.
      - Assert: `result.Count == 0`; `_clientDb.Measurements.Count() == 5` (no change).

  - [x] 8.3 **Mocking `IHttpClientFactory`** — use `MockHttpMessageHandler` pattern:
    ```csharp
    // Use a simple delegating handler to return canned responses.
    var handler = new MockHttpMessageHandler(request =>
    {
        var pullResponse = new ClientMeasurementPullResponse { Measurements = dtos, Total = dtos.Count };
        var json = System.Text.Json.JsonSerializer.Serialize(pullResponse);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    });
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
    var httpClientFactory = Substitute.For<IHttpClientFactory>(); // or use a manual stub
    httpClientFactory.CreateClient("ServerService").Returns(httpClient);
    ```
    **OR**: Use a minimal manual `HttpClientFactory` stub (no NSubstitute required):
    ```csharp
    private class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
    ```
    **Recommendation: Use the `StubHttpClientFactory` pattern** — it requires no additional mocking
    library (already used in the project). Check if NSubstitute or Moq is already in
    `MicroservicesSync.Tests.csproj` before adding new packages.

  - [x] 8.4 **MockHttpMessageHandler**: Define as a simple inner helper class in the test file:
    ```csharp
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
    ```
  - [x] 8.5 **FK seeding for client tests**: Client pull inserts Measurement entities with UserId and
    CellId FKs. These references MUST exist in `_clientDb` before `AddRangeAsync` is called.
    Use the same full FK chain: User → Building → Room → Surface → Cell.
    See `MeasurementGenerationTests.cs` for the complete `SeedCellsAsync` helper pattern.
  - [x] 8.6 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [x] 8.7 Run `dotnet test` — all 31 existing tests pass; new pull tests pass (36 total: 31 + 5).
  - [x] 8.8 Manual Docker smoke test: `docker-compose up --build`, then:
    1. Reset all clients (POST `/api/v1/admin/reset` on each)
    2. Pull reference data on all clients
    3. Generate measurements on each client
    4. Push from every client (`POST /api/v1/measurements/push` on each)
    5. Verify ServerService Measurements grid shows all expected records
    6. Trigger pull on each client (`POST /api/v1/measurements/pull`)
    7. Verify each client Measurements grid matches ServerService — same count and IDs

## Dev Notes

### Architecture Constraints (MUST follow)

- **`GET /api/v1/sync/measurements/pull` on ServerService** — the route prefix `[Route("api/v1/sync")]`
  is already on `SyncMeasurementsController`. Add `[HttpGet("measurements/pull")]` to the same class.
  Full route: `GET /api/v1/sync/measurements/pull`. This is the architecture-canonical URL from the PRD.
  Do NOT create a new controller for this.

- **In-memory batching on ClientService, NOT multiple HTTP requests**: The client calls the server
  pull endpoint once and receives the full list. ClientService internally partitions the list into
  `BatchSize` groups in memory and applies them inside ONE SQLite transaction. Do NOT design the
  client to make multiple HTTP GET requests, one per batch.

- **Single-transaction pattern for ClientService pull**: Use `await using var transaction = await _db.Database.BeginTransactionAsync(...)`.
  Call `CommitAsync` in try block. Call `RollbackAsync` explicitly in catch (same pattern as Story 2.2
  push controller on ServerService). The `await using` ensures disposal even if `RollbackAsync` is
  not reached.

- **Skip-existing semantics, NOT delete-and-reinsert**: ClientService MUST NOT delete existing
  measurements before a pull. Instead, load the server's ID set, diff against local IDs, and insert
  only the genuinely new ones. This preserves the client's own `SyncedAt` values (set after push)
  and avoids FK cascade issues or data loss if half of an insert batch fails.

- **`SyncedAt` semantics for pulled measurements**: Measurements pulled from ServerService and
  inserted locally get `SyncedAt = null` on the ClientService side. These are other clients'
  measurements that THIS client never pushed. The `SyncedAt` field on ClientService means
  "pushed by this client to ServerService." Other-client measurements never need `SyncedAt` set.

- **`IOptions<SyncOptions>` in `MeasurementSyncService`**: Adding this constructor parameter does
  NOT require any `Program.cs` change. The DI container already has `IOptions<SyncOptions>` registered
  via `builder.Services.Configure<SyncOptions>(...)`. When `MeasurementSyncService` is resolved
  as a scoped service, DI automatically injects the pre-configured `IOptions<SyncOptions>` instance.

- **FK constraint satisfaction on pull**: ClientService can only insert measurements whose `UserId`
  and `CellId` FK parents exist in its local SQLite DB. These are populated during the reference-data
  pull (startup auto-pull or `POST /api/v1/admin/pull-reference-data`). The pull must occur first.
  This is a precondition, not code logic — document it in the UI (Task 7 prerequisite note).

- **`GetFromJsonAsync<T>` on `HttpClient`**: From `System.Net.Http.Json` namespace (already available
  transitively via `Microsoft.AspNetCore.App`). No new NuGet package required. Call:
  ```csharp
  await http.GetFromJsonAsync<ClientMeasurementPullResponse>("api/v1/sync/measurements/pull", cancellationToken)
  ```
  Note: `GetFromJsonAsync` returns `null` if the response body is null/empty. The `null` check in
  `PullAsync` step 1 handles this defensively.

- **`AsNoTracking()` for existence check in PullAsync**: The ID existence query must use
  `AsNoTracking()` to avoid polluting the EF change tracker before the insert transaction begins.
  Follow the same pattern as `PushAsync` step 1.

- **No RowVersion/ConcurrencyStamp issues on insert**: Inserting brand-new measurement entities
  (from the server's pull data) into SQLite follows the same pattern as Story 2.1's
  `MeasurementGenerationService`. The `ConcurrencyStamp` shadow property has `HasDefaultValue(0L)`
  configured in `ClientDbContext.OnModelCreating` — do NOT set it manually. `AddRangeAsync` will use
  the default value automatically.

- **No auth on sync endpoints**: Per architecture ADR, sync endpoints are open. Do NOT add
  `[Authorize]`, bearer tokens, or any auth to the pull endpoint.

- **Button ID uniqueness in `Index.cshtml`**: The Administration section already uses `btnPull`
  and `pullResult` IDs for the "Pull Reference Data" button. Use `btnPullMeasurements` and
  `pullMeasurementsResult` for the new "Pull Measurements" button to avoid JavaScript DOM conflicts.

- **`IHttpClientFactory.CreateClient("ServerService")` is already registered** in
  `ClientService/Program.cs` with the server base address. Do NOT add a new `AddHttpClient` call.

### Key Implementation Files

| File | Action | Notes |
|---|---|---|
| `ServerService/Models/Sync/MeasurementPullDto.cs` | **CREATE** | `MeasurementPullItemDto`, `MeasurementPullResponse` |
| `ServerService/Controllers/SyncMeasurementsController.cs` | **MODIFY** | Add `GET measurements/pull` action + `using Microsoft.EntityFrameworkCore` if missing |
| `ClientService/Models/Sync/MeasurementPullDto.cs` | **CREATE** | `ClientMeasurementPullResponse`, `ClientMeasurementPullItemDto` (internal sealed) |
| `ClientService/Services/MeasurementSyncService.cs` | **MODIFY** | Add `IOptions<SyncOptions>` ctor param + `_batchSize` field + `PullAsync()` method + `MeasurementPullResult` record |
| `ClientService/Controllers/MeasurementsController.cs` | **MODIFY** | Add `[HttpPost("pull")]` action |
| `ClientService/Views/Home/Index.cshtml` | **MODIFY** | Add "Pull Measurements" button + `pullMeasurements()` JS function |
| `MicroservicesSync.Tests/Measurements/MeasurementPullTests.cs` | **CREATE** | 5 test cases (2 server + 3 client) |

### Reference Files (read before writing)

- [ServerService/Controllers/SyncMeasurementsController.cs](../../../MicroservicesSync/ServerService/Controllers/SyncMeasurementsController.cs)
  — Existing `SyncMeasurementsController`. Add `Pull` endpoint alongside the existing `Push` endpoint.
  Match the controller structure: constructor fields, `AsNoTracking`, logger pattern.
- [ServerService/Models/Sync/MeasurementPushDto.cs](../../../MicroservicesSync/ServerService/Models/Sync/MeasurementPushDto.cs)
  — Model for `MeasurementPullDto.cs` naming and structure. Match namespace, style, and DTO naming.
- [ClientService/Services/MeasurementSyncService.cs](../../../MicroservicesSync/ClientService/Services/MeasurementSyncService.cs)
  — Existing `MeasurementSyncService`. Add `PullAsync` and `MeasurementPullResult` to this file.
  Match constructor pattern, `// NOTE (M2.2):` comment style, `AsNoTracking`, `AddRangeAsync` pattern.
- [ClientService/Models/Sync/MeasurementPushDto.cs](../../../MicroservicesSync/ClientService/Models/Sync/MeasurementPushDto.cs)
  — Pattern for `internal sealed` client DTOs. Match `ClientMeasurementPush*` naming convention with
  `ClientMeasurementPull*` for the pull DTOs.
- [ClientService/Controllers/MeasurementsController.cs](../../../MicroservicesSync/ClientService/Controllers/MeasurementsController.cs)
  — Existing controller with `generate` and `push`. Add `pull` action. Match error-handling shape
  (try/catch with `InvalidOperationException → BadRequest`, `Exception → 500`).
- [ClientService/Views/Home/Index.cshtml](../../../MicroservicesSync/ClientService/Views/Home/Index.cshtml)
  — Existing home view. Match the "Push Measurements" block exactly in style and appended after it.
  Be careful of existing `btnPull` / `pullResult` IDs in the Administration section.
- [MicroservicesSync.Tests/Measurements/MeasurementPushTests.cs](../../../MicroservicesSync/MicroservicesSync.Tests/Measurements/MeasurementPushTests.cs)
  — Copy `TestableServerDbContext`, `SqliteConnection + EnsureCreated()` setup pattern, `SeedServerReferenceDataAsync`
  helper, and `IDisposable` cleanup for server-side pull tests.
- [MicroservicesSync.Tests/Measurements/MeasurementGenerationTests.cs](../../../MicroservicesSync/MicroservicesSync.Tests/Measurements/MeasurementGenerationTests.cs)
  — Copy `ClientDbContext` in-memory SQLite setup and the full FK seeding chain (User → Building → Room → Surface → Cell) for client-side pull tests.
- [Sync.Application/Options/SyncOptions.cs](../../../MicroservicesSync/Sync.Application/Options/SyncOptions.cs)
  — Shows `BatchSize = 5` default. Do NOT redefine this property.

### Previous Story Learnings (from Story 2.2)

**Patterns to follow:**
- `ExecuteUpdateAsync` is ONLY for bulk-updating existing tracked entities (used in PushAsync to set `SyncedAt`). For pull inserts, use `AddRangeAsync` + `SaveChangesAsync` inside the transaction loop — same pattern as ServerService's BatchedPush.
- `await using var transaction = await _db.Database.BeginTransactionAsync(...)` for the SQLite transaction scope (matches ServerService push pattern from Story 2.2).
- `Options.Create(new SyncOptions { BatchSize = 3 })` for IOptions in tests.
- `NullLogger<T>.Instance` for logger in tests.
- `IDisposable` + open `SqliteConnection` + `EnsureCreated()` test setup (preserved across test class).
- `DateTime.UtcNow` — ALWAYS, never `DateTime.Now`. Required for any timestamps.
- `Array.Empty<byte>()` for `RowVersion` on server-side test Measurement entities (SQLite in tests doesn't auto-generate).
- The `TestableServerDbContext` pattern (overrides `RowVersion` to `ValueGeneratedNever`) must be present in the pull test file — it is `internal sealed` and private to `MeasurementPushTests.cs`, so declare a fresh copy in `MeasurementPullTests.cs`.
- Full FK chain for seeding in tests: User → Building → Room → Surface → Cell → Measurement. SQLite DOES enforce FK constraints when `EnsureCreated()` is used.

**New pattern for Story 2.3:**
- `StubHttpClientFactory` / `MockHttpMessageHandler` — needed for client-side pull tests. No mocking
  library required; a simple `HttpMessageHandler` subclass handles canned HTTP responses.
- `GetFromJsonAsync<T>` for the GET pull request — returns deserialized object or null.
- Skip-existing logic: load existing IDs with `AsNoTracking().Where().Select(m => m.Id).ToListAsync()`,
  convert to `HashSet<Guid>` in memory, then filter the server response list before batching.

**Problems to avoid:**
- Using `btnPull` / `pullResult` IDs in the view — already taken by "Pull Reference Data" button.
- Forgetting to add `IOptions<SyncOptions>` to `MeasurementSyncService` constructor — PullAsync fails to compile without `_batchSize`.
- Deleting existing client measurements before inserting pull results — breaks `SyncedAt` semantics and convergence correctness.
- Making multiple HTTP GET calls — one call, full list, client-side in-memory batching only.
- Setting `SyncedAt` on pulled measurement entities — leave it `null` (these are not pushed by THIS client).
- Using duplicate `using` statements in `MeasurementSyncService.cs` — check existing ones first.

### Project Context Reference

This story is part of Epic 2: Multi-Client Sync Scenarios. It completes the fundamental push → pull
convergence pipeline: clients generate measurements, push to server, then pull the consolidated set
so all participants hold identical data. This is the core verification mechanism for FR6 and FR8.

**Convergence invariant after this story**: After all clients push and then pull, every ClientService
instance and ServerService hold the same set of measurement GUIDs. Verifiable at the DB level.

See [project-context.md](../../../_bmad-output/project-context.md) for the canonical critical rules
and patterns that govern all implementation in this project.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

_None — clean implementation, no debugging required._

### Completion Notes List

- **Task 1**: Created `ServerService/Models/Sync/MeasurementPullDto.cs` with `MeasurementPullItemDto` and `MeasurementPullResponse` (no `SyncedAt` field per spec).
- **Task 2**: Added `[HttpGet("measurements/pull")]` action to `SyncMeasurementsController`. Added `using Microsoft.EntityFrameworkCore;` (was missing). Read-only query using `AsNoTracking().Select().ToListAsync()` — no transaction.
- **Task 3**: Created `ClientService/Models/Sync/MeasurementPullDto.cs` with `internal sealed` `ClientMeasurementPullResponse` and `ClientMeasurementPullItemDto`, using `Client` prefix for disambiguation.
- **Task 4**: Updated `MeasurementSyncService` — added `using Microsoft.Extensions.Options;`, `using Sync.Application.Options;`, `using Sync.Domain.Entities;`; added `IOptions<SyncOptions>` constructor param; added `_batchSize` field; implemented `PullAsync()` with one HTTP GET call, skip-existing dedup, single SQLite transaction, in-memory batch partitioning; added `MeasurementPullResult` record. Also fixed `MeasurementPushTests.cs` to pass new 4-param constructor.
- **Task 5**: Added `[HttpPost("pull")]` action to `MeasurementsController` matching existing error-handling shape (`InvalidOperationException → BadRequest`, `Exception → 500`).
- **Task 6**: No `Program.cs` changes needed — `IOptions<SyncOptions>` already registered via `Configure<SyncOptions>`.
- **Task 7**: Added "Pull Consolidated Measurements" section to `Index.cshtml` after "Push Measurements" block; uses `btnPullMeasurements` / `pullMeasurementsResult` IDs (distinct from existing `btnPull` / `pullResult` for reference data).
- **Task 8**: Created `MicroservicesSync.Tests/Measurements/MeasurementPullTests.cs` with 5 test cases (2 server-side, 3 client-side). Uses `TestableServerDbContextForPull`, `StubHttpClientFactory`, `MockHttpMessageHandler`, FK seeding. Build: 0 errors, 0 warnings. Tests: 36 total, 36 passed (5 new pull tests).

### Code Review Fixes (2026-03-07)

- **HIGH-1 (AC3 rollback untested)**: Added `PullAsync_TransactionRollsBack_WhenBatchFails` test to `MeasurementPullTests.cs`. Uses a duplicate GUID across two batches to trigger an EF tracking conflict mid-pull, then asserts the Measurements table is empty after rollback. Test count: 36 → 37.
- **HIGH-2 (TOCTOU race)**: Moved the `existingIds` existence query inside the transaction in `MeasurementSyncService.PullAsync`. The transaction now opens before the read, so concurrent pull requests cannot both pass the existence check and race to insert the same IDs.
- **MEDIUM-1 (HTTP error body lost)**: Replaced `GetFromJsonAsync` with `GetAsync` + explicit `IsSuccessStatusCode` guard + `ReadAsStringAsync` on failure + `ReadFromJsonAsync` on success. Error body is now logged and surfaced, matching the `PushAsync` pattern.
- **MEDIUM-2 (wrong baseline test count in story)**: Corrected Task 8.7 — updated "35 existing" to "31 existing" (actual baseline) and "~40 total" to "36 total".
- **LOW-1 (response.Total vs Measurements.Count)**: Changed the "all already present" log message to use `response.Measurements.Count` consistently.

### File List

- `ServerService/Models/Sync/MeasurementPullDto.cs` — **CREATED**
- `ServerService/Controllers/SyncMeasurementsController.cs` — **MODIFIED** (added `Pull` endpoint + `using Microsoft.EntityFrameworkCore`)
- `ClientService/Models/Sync/MeasurementPullDto.cs` — **CREATED**
- `ClientService/Services/MeasurementSyncService.cs` — **MODIFIED** (added `IOptions<SyncOptions>` ctor param, `_batchSize` field, `PullAsync()` method, `MeasurementPullResult` record, 3 new usings)
- `ClientService/Controllers/MeasurementsController.cs` — **MODIFIED** (added `[HttpPost("pull")]` action)
- `ClientService/Views/Home/Index.cshtml` — **MODIFIED** (added Pull Measurements button + JS function)
- `MicroservicesSync.Tests/Measurements/MeasurementPullTests.cs` — **CREATED** (+ **MODIFIED** in code review: added rollback test)
- `MicroservicesSync.Tests/Measurements/MeasurementPushTests.cs` — **MODIFIED** (updated `Push_NoPendingMeasurements_ReturnsZeroCount` to pass new `IOptions<SyncOptions>` constructor param)
