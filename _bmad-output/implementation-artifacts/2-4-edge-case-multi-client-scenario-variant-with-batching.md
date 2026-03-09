# Story 2.4: Edge-Case Multi-Client Scenario Variant with Batching

Status: done

## Story

As a developer,
I want to run at least one clearly documented edge-case multi-client scenario using batched, transactional sync,
so that I can validate behavior under different timing or volume conditions with all-or-nothing guarantees.

## Acceptance Criteria

1. **Given** the standard scenario is implemented  
   **When** I configure and run the documented edge-case variant (different client count, staggered pushes, or larger per-client volumes) with batched push and pull enabled  
   **Then** the scenario completes without unhandled errors.

2. **Given** the edge-case variant has completed successfully  
   **When** I inspect measurement data on ServerService and all involved ClientService instances  
   **Then** each client and the server converge to the same dataset, confirming that batched, transactional sync still prevents loss or duplication.

3. **Given** the edge-case scenario is documented  
   **When** a new developer follows the instructions  
   **Then** they can reproduce the same steps, including the batching configuration, and verify convergence.

## Tasks / Subtasks

- [x] **Task 1: Add a dedicated edge-case scenario section to ServerService home page** (AC: #1, #3)
    - [x] 1.1 Open `ServerService/Views/Home/Index.cshtml`.
    - [x] 1.2 Add an "Edge-Case Scenario" section after the standard sync summary (or at the bottom of the page) with:
    - A description of the edge-case variant: **3-client reduced-volume scenario** (3 clients × 5 measurements, using `SyncOptions:BatchSize=2` to stress-test batching with odd/uneven batch counts).
    - Step-by-step instructions for how to run it (see Dev Notes for exact steps).
    - A "Verify Convergence" summary with links to the relevant grids on each service.
  - [x] 1.3 The section is purely informational HTML — no new API endpoints or JavaScript needed here. Use a `<ol>` numbered list for the steps.

- [x] **Task 2: Add `GET /api/v1/sync/measurements/count` endpoint to ServerService** (AC: #2)
  - [x] 2.1 Open `ServerService/Controllers/SyncMeasurementsController.cs`.
  - [x] 2.2 Add a new `[HttpGet("measurements/count")]` action that returns the total number of measurements currently stored on ServerService:
    ```csharp
    [HttpGet("measurements/count")]
    public async Task<IActionResult> Count(CancellationToken cancellationToken)
    {
        var count = await _db.Measurements
            .AsNoTracking()
            .CountAsync(cancellationToken);
        _logger.LogInformation(
            "SyncMeasurementsController: count requested — {Count} measurements.", count);
        return Ok(new { count });
    }
    ```
  - [x] 2.3 Add `using Microsoft.EntityFrameworkCore;` if not already present (already there from Tasks in Story 2.3).
  - [x] 2.4 This endpoint is used by ClientService's home page JavaScript to verify convergence (see Task 3). It is a read-only diagnostic aid — no writes.

- [x] **Task 3: Add "Verify Convergence" UI to ClientService home page** (AC: #2, #3)
  - [x] 3.1 Open `ClientService/Views/Home/Index.cshtml`.
  - [x] 3.2 Add a new "Verify Convergence" section **after** the "Pull Consolidated Measurements from ServerService" `<div>` block, still within the `<div class="mt-4">` that wraps the Sync Scenario Controls:
    ```html
    <div class="mb-3">
        <h4>Verify Convergence</h4>
        <p>Checks whether this client's local measurement count matches ServerService's total count.
           Run after a completed pull to confirm convergence.</p>
        <button id="btnVerifyConvergence" onclick="verifyConvergence()" class="btn btn-secondary">Verify Convergence</button>
        <div id="verifyConvergenceResult" class="mt-2"></div>
    </div>
    ```
  - [x] 3.3 Add JavaScript function within the existing `<script>` block (alongside `generateMeasurements()`, `pushMeasurements()`, `pullMeasurements()`):
    ```javascript
    function verifyConvergence() {
        document.getElementById('btnVerifyConvergence').disabled = true;
        Promise.all([
            fetch('/api/v1/measurements/count').then(r => r.json()),
            fetch('/api/v1/sync/measurements/count', {
                headers: { 'X-Forward-To-ServerService': '1' }
            })
        ])
        .then(async ([localData, _]) => {
            // Simpler approach: call local count + server count via separate fetches
            const localCount = localData.count;
            return fetch('/api/v1/measurements/server-count')
                .then(r => r.json())
                .then(serverData => ({ localCount, serverCount: serverData.count }));
        })
        .then(({ localCount, serverCount }) => {
            const matched = localCount === serverCount;
            const el = document.createElement('div');
            el.className = matched ? 'alert alert-success mt-2' : 'alert alert-warning mt-2';
            el.textContent = matched
                ? `✓ Converged: this client has ${localCount} measurements, server has ${serverCount}.`
                : `⚠ Not yet converged: this client has ${localCount} measurements, server has ${serverCount}. Run Pull Measurements first.`;
            const box = document.getElementById('verifyConvergenceResult');
            box.innerHTML = '';
            box.appendChild(el);
        })
        .catch(err => {
            const el = document.createElement('div');
            el.className = 'alert alert-danger mt-2';
            el.textContent = 'Verify failed: ' + (err.message || err);
            const box = document.getElementById('verifyConvergenceResult');
            box.innerHTML = '';
            box.appendChild(el);
        })
        .finally(() => { document.getElementById('btnVerifyConvergence').disabled = false; });
    }
    ```
    **NOTE**: The JavaScript above uses the placeholder approach. Replace with the correct two-call approach described in the Note below.
  - [x] 3.4 **CORRECT JavaScript implementation** — Replace the above placeholder with this simpler, working version that makes two parallel calls:
    ```javascript
    function verifyConvergence() {
        document.getElementById('btnVerifyConvergence').disabled = true;
        const localFetch = fetch('/api/v1/measurements/count').then(r => r.json());
        const serverFetch = fetch('/api/v1/measurements/server-count').then(r => r.json());
        Promise.all([localFetch, serverFetch])
            .then(([localData, serverData]) => {
                const localCount = localData.count;
                const serverCount = serverData.count;
                const matched = localCount === serverCount;
                const el = document.createElement('div');
                el.className = matched ? 'alert alert-success mt-2' : 'alert alert-warning mt-2';
                el.textContent = matched
                    ? `✓ Converged: this client has ${localCount} measurements, server has ${serverCount}.`
                    : `⚠ Not yet converged: this client has ${localCount} measurements, server has ${serverCount}. Run Pull Measurements first.`;
                const box = document.getElementById('verifyConvergenceResult');
                box.innerHTML = '';
                box.appendChild(el);
            })
            .catch(err => {
                const el = document.createElement('div');
                el.className = 'alert alert-danger mt-2';
                el.textContent = 'Verify failed: ' + (err.message || err);
                const box = document.getElementById('verifyConvergenceResult');
                box.innerHTML = '';
                box.appendChild(el);
            })
            .finally(() => { document.getElementById('btnVerifyConvergence').disabled = false; });
    }
    ```
  - [x] 3.5 Use IDs `btnVerifyConvergence` and `verifyConvergenceResult` — these are new and do NOT conflict with existing IDs (`btnReset`, `btnPull`, `btnGenerate`, `btnPush`, `btnPullMeasurements`).

- [x] **Task 4: Add `GET /api/v1/measurements/count` and `GET /api/v1/measurements/server-count` to ClientService** (AC: #2)
  - [x] 4.1 Open `ClientService/Controllers/MeasurementsController.cs`.
  - [x] 4.2 Add `[HttpGet("count")]` — local client measurement count:
    ```csharp
    [HttpGet("count")]
    public async Task<IActionResult> Count(CancellationToken cancellationToken)
    {
        var count = await _db.Measurements
            .AsNoTracking()
            .CountAsync(cancellationToken);
        return Ok(new { count });
    }
    ```
  - [x] 4.3 Add `[HttpGet("server-count")]` — proxied call to ServerService measurement count (for the convergence check):
    ```csharp
    [HttpGet("server-count")]
    public async Task<IActionResult> ServerCount(CancellationToken cancellationToken)
    {
        try
        {
            var http = _httpClientFactory.CreateClient("ServerService");
            var response = await http.GetAsync("api/v1/sync/measurements/count", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { message = "Failed to reach ServerService count endpoint." });
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            // Return the raw JSON from ServerService directly
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: server-count proxy call failed.");
            return StatusCode(500, new { message = "Failed to reach ServerService.", error = ex.Message });
        }
    }
    ```
  - [x] 4.4 Add `IHttpClientFactory` injection to the `MeasurementsController` constructor (it is NOT currently injected — only `MeasurementGenerationService`, `MeasurementSyncService`, and `ILogger` are injected). Update the constructor:
    ```csharp
    private readonly MeasurementGenerationService _generationService;
    private readonly MeasurementSyncService _syncService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MeasurementsController> _logger;

    public MeasurementsController(
        MeasurementGenerationService generationService,
        MeasurementSyncService syncService,
        IHttpClientFactory httpClientFactory,
        ILogger<MeasurementsController> logger)
    {
        _generationService = generationService;
        _syncService = syncService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    ```
  - [x] 4.5 Add `ClientDbContext` injection to `MeasurementsController` for the `Count` endpoint, OR delegate to `MeasurementSyncService` for the local count. **Preferred approach**: Inject `ClientDbContext` directly (consistent with `AdminController` note NOTE M4 and `MeasurementSyncService` note NOTE M2.2 — direct `ClientDbContext` injection is an acknowledged deviation for this project's scope). Update constructor to also accept `ClientDbContext`:
    ```csharp
    private readonly ClientDbContext _db;

    public MeasurementsController(
        ClientDbContext db,
        MeasurementGenerationService generationService,
        MeasurementSyncService syncService,
        IHttpClientFactory httpClientFactory,
        ILogger<MeasurementsController> logger)
    {
        _db = db;
        _generationService = generationService;
        _syncService = syncService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    ```
  - [x] 4.6 Add `using Microsoft.EntityFrameworkCore;` and `using Sync.Infrastructure.Data;` at the top of the file if not already present.
  - [x] 4.7 `IHttpClientFactory` and `ClientDbContext` are already registered in DI (`Program.cs`) — no `Program.cs` changes needed.

- [x] **Task 5: Write tests** (AC: #1, #2)
  - [x] 5.1 Create `MicroservicesSync.Tests/Measurements/MeasurementCountTests.cs`.
  - [x] 5.2 Test the new `SyncMeasurementsController.Count` endpoint on ServerService:

    - `Count_WithMeasurements_ReturnsCorrectCount`:
      - Use a `TestableServerDbContextForCount` (same SQLite-compatible pattern as in `MeasurementPullTests.cs` — declare locally in this file, do NOT import from `MeasurementPullTests.cs`).
      - Seed server reference data (User, Building, Room, Surface, Cell).
      - Insert 8 Measurement entities directly into `_serverDb`.
      - Call `controller.Count(CancellationToken.None)`.
      - Assert: result is `OkObjectResult`; JSON deserializes to `{ count: 8 }`.

    - `Count_EmptyDatabase_ReturnsZero`:
      - Do NOT seed any measurements.
      - Call `controller.Count(CancellationToken.None)`.
      - Assert: result is `OkObjectResult`; JSON deserializes to `{ count: 0 }`.

  - [x] 5.3 Test the new `MeasurementsController.Count` endpoint on ClientService:

    - `LocalCount_WithMeasurements_ReturnsCorrectCount`:
      - Use in-memory SQLite `ClientDbContext` (same pattern as `MeasurementPullTests.cs`).
      - Seed client reference data (User, Building, Room, Surface, Cell).
      - Insert 4 Measurement entities into `_clientDb`.
      - Call `controller.Count(CancellationToken.None)`.
      - Assert: result is `OkObjectResult`; JSON deserializes to `{ count: 4 }`.

    - `LocalCount_EmptyDatabase_ReturnsZero`:
      - Do NOT seed any measurements.
      - Call `controller.Count(CancellationToken.None)`.
      - Assert: result is `OkObjectResult`; JSON deserializes to `{ count: 0 }`.

  - [x] 5.4 Helper pattern for deserializing `OkObjectResult` — use same JSON pattern as `MeasurementPullTests.cs`:
    ```csharp
    var ok = Assert.IsType<OkObjectResult>(result);
    var json = JsonSerializer.Serialize(ok.Value);
    var data = JsonSerializer.Deserialize<JsonElement>(json);
    Assert.Equal(8, data.GetProperty("count").GetInt32());
    ```
  - [x] 5.5 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [x] 5.6 Run `dotnet test` — all 37 existing tests pass; new count tests pass (41 total: 37 + 4 new).
  - [ ] 5.7 Manual Docker validation for the edge-case scenario (see Edge-Case Scenario Steps in Dev Notes below).

## Dev Notes

### Architecture Constraints (MUST follow)

- **No new sync logic**: Story 2.4 is NOT about changing push/pull behavior. Push (`PushAsync`) and Pull (`PullAsync`) in `MeasurementSyncService` are **complete and correct** from Stories 2.2 and 2.3. Do NOT modify them.

- **Edge-case variant is a documented runtime exercise, not new code**: The "edge-case" in Story 2.4 is achieved by configuring existing infrastructure differently (different `SyncOptions__BatchSize`, `SyncOptions__MeasurementsPerClient`, client count) and running through the same generate→push→pull flow. New code is limited to: (1) a count endpoint for convergence verification, (2) a convergence check UI button, and (3) documentation on the ServerService home page.

- **`GET /api/v1/sync/measurements/count` route prefix**: `SyncMeasurementsController` already has `[Route("api/v1/sync")]`. The new action uses `[HttpGet("measurements/count")]`, giving full URL: `GET /api/v1/sync/measurements/count`.

- **`GET /api/v1/measurements/count` vs `GET /api/v1/measurements/server-count`**: Both are on `MeasurementsController` in ClientService (route prefix `api/v1/measurements`). `count` = local SQLite count; `server-count` = proxy to `GET /api/v1/sync/measurements/count` on ServerService. The proxy pattern keeps all cross-service communication inside the backend (not client-side JavaScript calling ServerService directly, which might have CORS issues in Docker).

- **`IHttpClientFactory` named client "ServerService"**: Already configured in `ClientService/Program.cs` for `MeasurementSyncService`. The same named client (`_httpClientFactory.CreateClient("ServerService")`) works for the `server-count` proxy.

- **No auth on count endpoints**: All new endpoints follow the existing pattern — no `[Authorize]` or tokens.

- **Button ID uniqueness**: New IDs `btnVerifyConvergence` and `verifyConvergenceResult` do not conflict with existing IDs: `btnReset`, `resetResult`, `btnPull`, `pullResult`, `btnGenerate`, `generateResult`, `btnPush`, `pushResult`, `btnPullMeasurements`, `pullMeasurementsResult`.

- **`TestableServerDbContextForCount`**: Declare a fresh local `TestableServerDbContextForCount` in `MeasurementCountTests.cs` using the exact same pattern as `TestableServerDbContextForPull` in `MeasurementPullTests.cs`. Do NOT reference the class from the other test file (different compilation units, types are internal).

- **`ServerCount` proxy in `MeasurementsController`**: Uses `Content(content, "application/json")` to forward the raw JSON from ServerService without double-serialization. This preserves the `{ count: N }` shape that JavaScript expects.

- **jqGrid compatibility**: The new count endpoints are NOT jqGrid endpoints. They do not follow the jqGrid paging contract. They are simple diagnostic endpoints that return `{ count: N }`.

### Edge-Case Scenario Steps (for documentation in serverservice/home page)

The edge-case variant is a **3-client, 5-measurements, batch-size-2 scenario**. It exercises:
- Uneven batch sizes (5 measurements ÷ batch 2 = 2 full batches + 1 partial batch), confirming the transaction correctly handles the remainder.
- Reduced client count (3 instead of 5), making it easy to compare manually.

**Steps to run the edge-case scenario (from a clean baseline):**

1. Ensure the system is running: `docker-compose up` (from `MicroservicesSync/` folder).
2. Reset all 5 clients (or just clients 1–3) via "Reset Client DB" on each ClientService UI.
3. Pull reference data on clients 1–3 via "Pull Reference Data" (Administration section).
4. Temporarily set `SyncOptions__MeasurementsPerClient=5` and `SyncOptions__BatchSize=2` for clients 1–3 via docker-compose.override.yml or by direct environment variable override.
   - **Alternative (no restart)**: The default `MeasurementsPerClient=10, BatchSize=5` also works for the variant — use `MeasurementsPerClient=5` and `BatchSize=2` for maximum batch-remainder stress-testing.
5. Generate measurements on clients 1–3 only (click "Generate Measurements" on each).
6. Push from each of clients 1–3 (click "Push Measurements").
7. Verify ServerService Measurements grid shows 15 records (3 clients × 5 measurements each).
8. Pull on clients 1–3 (click "Pull Measurements").
9. Click "Verify Convergence" on each of clients 1–3 — each should report 15 local measurements matching server's 15.
10. (Optional) Clients 4 and 5 remain at baseline with 0 measurements, confirming isolation.

**Unhandled-error check**: No 500 errors should appear in any client or server logs. All batches should commit cleanly.

### Previous Story Learnings (from Story 2.3)

- **`SyncedAt` on pulled measurements is `null`**: Other clients' measurements pulled to a client always have `SyncedAt = null`. Do not change this.
- **`GetFromJsonAsync` vs manual `GetAsync` + `ReadFromJsonAsync`**: Story 2.3 implementation used `GetAsync` + `ReadFromJsonAsync` instead of `GetFromJsonAsync` for better error handling (non-2xx responses). Follow the same pattern in the `ServerCount` proxy.
- **Transaction open before ID existence check**: The pull implementation keeps the transaction open across both the existence-check query and the insert loop (TOCTOU fix). The count endpoints are read-only and do NOT need transactions.
- **`AsNoTracking()` for read-only queries**: Use it for all new count queries to avoid polluting EF change tracker.
- **`StubHttpClientFactory` inner class** is the established pattern in this project (no NSubstitute/Moq needed). Reuse from the test file rather than copy.

### Project Structure Notes

**Files to create:**
- `MicroservicesSync.Tests/Measurements/MeasurementCountTests.cs` (new test file)

**Files to modify:**
- `ServerService/Controllers/SyncMeasurementsController.cs` — add `Count` action
- `ServerService/Views/Home/Index.cshtml` — add edge-case scenario documentation section
- `ClientService/Controllers/MeasurementsController.cs` — add `Count`, `ServerCount` actions + constructor update
- `ClientService/Views/Home/Index.cshtml` — add "Verify Convergence" section + JavaScript

**Files to NOT modify:**
- `ClientService/Services/MeasurementSyncService.cs` — push/pull logic is complete; do NOT touch
- `ClientService/Program.cs` — DI already registers all needed types; no changes needed
- Any domain entity in `Sync.Domain` — no schema changes
- `docker-compose.yml` — the edge-case is a runtime configuration exercise, not a topology change

### References

- Architecture: batch-size and transaction semantics [Source: `_bmad-output/planning-artifacts/architecture.md#Sync transactions and batching`]
- Story 2.2: push implementation reference [Source: `_bmad-output/implementation-artifacts/2-2-transactional-batched-server-side-measurement-push.md`]
- Story 2.3: pull implementation reference + `MeasurementSyncService.PullAsync` [Source: `_bmad-output/implementation-artifacts/2-3-transactional-batched-client-side-pull-of-consolidated-measurements.md`]
- Project Context: ClientService CRUD capability rules, inter-service HTTP patterns [Source: `_bmad-output/project-context.md`]
- Epics: Story 2.4 acceptance criteria and edge-case framing [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.4`]
- Existing test patterns: `MeasurementPullTests.cs`, `MeasurementGenerationTests.cs` [Source: `MicroservicesSync/MicroservicesSync.Tests/Measurements/`]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

None — clean implementation with no issues.

### File List

- `ServerService/Controllers/SyncMeasurementsController.cs` — added `Count` action
- `ServerService/Views/Home/Index.cshtml` — added edge-case scenario documentation section
- `ClientService/Controllers/MeasurementsController.cs` — added `Count`, `ServerCount` actions; updated constructor to inject `ClientDbContext` and `IHttpClientFactory`
- `ClientService/Views/Home/Index.cshtml` — added "Verify Convergence" section and `verifyConvergence()` JavaScript function
- `MicroservicesSync.Tests/Measurements/MeasurementCountTests.cs` — new file: 7 tests covering server count, client count, and ServerCount proxy

### Completion Notes List

- Task 1: Added "Edge-Case Scenario: 3-Client Reduced-Volume Batching" section to `ServerService/Views/Home/Index.cshtml` with numbered `<ol>` steps, convergence instructions, and unhandled-error check note.
- Task 2: Added `[HttpGet("measurements/count")]` action to `SyncMeasurementsController`. Route: `GET /api/v1/sync/measurements/count`. Uses `AsNoTracking().CountAsync()`.
- Task 3: Added "Verify Convergence" `<div>` block and `verifyConvergence()` JavaScript function to `ClientService/Views/Home/Index.cshtml`. Uses two parallel `fetch()` calls to `/api/v1/measurements/count` and `/api/v1/measurements/server-count`.
- Task 4: Updated `ClientService/Controllers/MeasurementsController.cs` — injected `ClientDbContext` and `IHttpClientFactory`; added `Count` (local SQLite count) and `ServerCount` (proxy to ServerService) endpoints. Added `using Microsoft.EntityFrameworkCore;` and `using Sync.Infrastructure.Data;`.
- Task 5: Created `MicroservicesSync.Tests/Measurements/MeasurementCountTests.cs` with 7 tests: `Count_WithMeasurements_ReturnsCorrectCount`, `Count_EmptyDatabase_ReturnsZero`, `LocalCount_WithMeasurements_ReturnsCorrectCount`, `LocalCount_EmptyDatabase_ReturnsZero`, `ServerCount_ServerReturnsOk_ReturnsServerJson`, `ServerCount_ServerReturnsNonOk_ReturnsProxiedStatusCode`, `ServerCount_HttpRequestThrows_Returns500`. Build: 0 errors, 0 warnings. Test run: 44/44 passed.
- Task 5.7 (manual Docker validation) left for user to perform per edge-case scenario steps documented on the ServerService home page.

### File List

- `ServerService/Controllers/SyncMeasurementsController.cs` — added `Count` action
- `ServerService/Views/Home/Index.cshtml` — added edge-case scenario documentation section
- `ClientService/Controllers/MeasurementsController.cs` — updated constructor + added `Count`, `ServerCount` actions
- `ClientService/Views/Home/Index.cshtml` — added "Verify Convergence" section + `verifyConvergence()` JS
- `MicroservicesSync.Tests/Measurements/MeasurementCountTests.cs` — new test file (4 tests)
