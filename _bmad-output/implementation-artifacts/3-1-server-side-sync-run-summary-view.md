# Story 3.1: Server-Side Sync Run Summary View

Status: ready-for-dev

## Story

As a developer,
I want a simple summary view of recent sync runs on ServerService,
so that I can quickly see which clients synced, when, and how many measurements were processed.

## Acceptance Criteria

1. **Given** ServerService has processed at least one sync push or pull  
   **When** I open the documented sync summary view on ServerService  
   **Then** I see a list of recent sync runs including at least timestamp, client/user identity, run type (push or pull), and number of measurements processed.

2. **Given** multiple clients have synced over time  
   **When** I sort or filter the summary view by client/user or date  
   **Then** I can focus on the subset of runs relevant to my investigation.

3. **Given** a sync run failed or partially succeeded  
   **When** I open the summary view  
   **Then** the failed run is clearly marked with an error or warning status and links or guidance to inspect logs for details.

## Tasks / Subtasks

- [ ] **Task 1: Add `SyncRun` entity to `Sync.Domain`** (AC: #1)
  - [ ] 1.1 Create `Sync.Domain/Entities/SyncRun.cs` with the following shape:
    ```csharp
    namespace Sync.Domain.Entities;

    /// <summary>Represents a record of a single sync push or pull operation processed by ServerService.</summary>
    public class SyncRun
    {
        public Guid Id { get; set; }
        public DateTime OccurredAt { get; set; }      // UTC timestamp of the run
        public string RunType { get; set; } = string.Empty;  // "push" | "pull"
        public Guid UserId { get; set; }               // FK → Users.Id
        public int MeasurementCount { get; set; }      // number of records processed
        public string Status { get; set; } = string.Empty;   // "success" | "failed"
        public string? ErrorMessage { get; set; }      // null on success; set on failure

        // Navigation (optional; no cascade delete required)
        public User User { get; set; } = null!;
    }
    ```
  - [ ] 1.2 `SyncRun` does NOT need a `RowVersion`/concurrency token — it is append-only and never updated after insert. Do NOT add `RowVersion` to this entity.
  - [ ] 1.3 Do NOT modify any existing entity (`Measurement`, `User`, `Building`, `Room`, `Surface`, `Cell`).

- [ ] **Task 2: Add `SyncRunConfiguration` to `Sync.Infrastructure`** (AC: #1)
  - [ ] 2.1 Create `Sync.Infrastructure/Data/Configurations/SyncRunConfiguration.cs`:
    ```csharp
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Sync.Domain.Entities;

    namespace Sync.Infrastructure.Data.Configurations;

    public class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
    {
        public void Configure(EntityTypeBuilder<SyncRun> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.RunType).HasMaxLength(10).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(10).IsRequired();
            builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
            builder.Property(x => x.OccurredAt).IsRequired();
            builder.Property(x => x.MeasurementCount).IsRequired();

            // FK → Users; no cascade delete (preserve SyncRun records even if User were removed)
            builder.HasOne(x => x.User)
                   .WithMany()
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Index for the summary view sort/filter patterns (by date, user, type)
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => x.UserId);
        }
    }
    ```
  - [ ] 2.2 No `RowVersion`/concurrency-token configuration is needed for `SyncRun` — the entity is immutable after insert.

- [ ] **Task 3: Register `SyncRun` in `ServerDbContext` and add migration** (AC: #1)
  - [ ] 3.1 Open `Sync.Infrastructure/Data/ServerDbContext.cs`.
  - [ ] 3.2 Add `public DbSet<SyncRun> SyncRuns => Set<SyncRun>();` after the existing `DbSet` declarations.
  - [ ] 3.3 In `OnModelCreating`, add `modelBuilder.ApplyConfiguration(new SyncRunConfiguration());` after the existing `ApplyConfiguration` calls (before the foreach that sets RowVersion). The foreach that sets RowVersion will harmlessly skip `SyncRun` because it has no `RowVersion` property.
  - [ ] 3.4 Generate a new SQL Server EF Core migration from the `ServerService` startup project:
    ```
    dotnet ef migrations add AddSyncRunTable --project Sync.Infrastructure --startup-project ServerService --output-dir Data/Migrations/Server
    ```
    Verify the generated migration creates the `SyncRuns` table with columns: `Id` (uniqueidentifier PK), `OccurredAt`, `RunType`, `Message`, `UserId` (FK), `MeasurementCount`, `Status`, `ErrorMessage`.
  - [ ] 3.5 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [ ] 3.6 `SyncRun` is server-side only. Do NOT add `SyncRun` or any related DbSet to `ClientDbContext`.

- [ ] **Task 4: Record `SyncRun` entries in `SyncMeasurementsController`** (AC: #1, #3)
  - [ ] 4.1 Open `ServerService/Controllers/SyncMeasurementsController.cs`.
  - [ ] 4.2 Modify the `Push` action to record a `SyncRun` entry after commit (on success) or after rollback (on failure). Insert inside the existing try/catch in the `Push` method. The `SyncRun` insert is NOT part of the measurement transaction — it is a separate, independent `SaveChangesAsync` call after the measurement transaction is committed or rolled back.

    **Insert on push success** (inside the `try` block, after `await transaction.CommitAsync(...)`):
    ```csharp
    // Record sync run summary (separate from measurement transaction)
    var syncRun = new Sync.Domain.Entities.SyncRun
    {
        Id = Guid.NewGuid(),
        OccurredAt = DateTime.UtcNow,
        RunType = "push",
        UserId = request.Measurements.First().UserId,
        MeasurementCount = request.Measurements.Count,
        Status = "success"
    };
    _db.SyncRuns.Add(syncRun);
    await _db.SaveChangesAsync(cancellationToken);
    ```

    **Insert on push failure** (inside the `catch` block, after `await transaction.RollbackAsync(...)`):
    ```csharp
    // Record failed sync run (best-effort; swallow exceptions)
    try
    {
        var failedRun = new Sync.Domain.Entities.SyncRun
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            RunType = "push",
            UserId = request.Measurements.FirstOrDefault()?.UserId ?? Guid.Empty,
            MeasurementCount = 0,
            Status = "failed",
            ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message
        };
        _db.SyncRuns.Add(failedRun);
        await _db.SaveChangesAsync(CancellationToken.None);
    }
    catch { /* best-effort; do not mask the original error */ }
    ```

  - [ ] 4.3 Modify the `Pull` action similarly. The Pull action currently has no try/catch — add one and record success/failure `SyncRun` entries. The `Pull` action has no inherent `UserId` since it returns ALL measurements. For Pull runs, use `UserId = Guid.Empty` with the understanding that Pull is a server-side read operation. 

    **Pull action refactor** — wrap the existing body in try/catch:
    ```csharp
    [HttpGet("measurements/pull")]
    public async Task<IActionResult> Pull(CancellationToken cancellationToken)
    {
        try
        {
            var measurements = await _db.Measurements
                .AsNoTracking()
                .Select(m => new MeasurementPullItemDto { ... })  // keep existing Select
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "SyncMeasurementsController: pull requested — returning {Count} measurements.",
                measurements.Count);

            // Record pull run summary
            var syncRun = new Sync.Domain.Entities.SyncRun
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                RunType = "pull",
                UserId = Guid.Empty,
                MeasurementCount = measurements.Count,
                Status = "success"
            };
            _db.SyncRuns.Add(syncRun);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new MeasurementPullResponse
            {
                Measurements = measurements,
                Total = measurements.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SyncMeasurementsController: pull failed.");
            try
            {
                var failedRun = new Sync.Domain.Entities.SyncRun
                {
                    Id = Guid.NewGuid(),
                    OccurredAt = DateTime.UtcNow,
                    RunType = "pull",
                    UserId = Guid.Empty,
                    MeasurementCount = 0,
                    Status = "failed",
                    ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message
                };
                _db.SyncRuns.Add(failedRun);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch { /* best-effort */ }
            return StatusCode(500, new { message = "Pull failed.", error = ex.Message });
        }
    }
    ```

  - [ ] 4.4 Do NOT change the batching logic, transaction scope, the existing `Count` action, or any other endpoints.
  - [ ] 4.5 **SCOPE GUARD**: Do NOT add a correlation ID or structured logging at this step. Story 3.4 will handle structured logging. Do NOT make any changes to `ClientService`.

- [ ] **Task 5: Add `SyncRunsController` to ServerService** (AC: #1, #2)
  - [ ] 5.1 Create `ServerService/Controllers/SyncRunsController.cs`:
    ```csharp
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Sync.Infrastructure.Data;

    namespace ServerService.Controllers;

    [ApiController]
    [Route("api/v1/sync-runs")]
    public class SyncRunsController : ControllerBase
    {
        private readonly ServerDbContext _db;
        private readonly ILogger<SyncRunsController> _logger;

        public SyncRunsController(ServerDbContext db, ILogger<SyncRunsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // NOTE (M3.1): SyncRunsController injects ServerDbContext directly.
        // Consistent with the acknowledged deviation in AdminController (NOTE M4) and
        // SyncMeasurementsController. Acceptable for this experiment scope.

        /// <summary>
        /// Returns recent sync runs, newest first. Optional query params: userId, runType (push|pull), limit (default 50, max 200).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRecent(
            [FromQuery] Guid? userId = null,
            [FromQuery] string? runType = null,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            if (limit <= 0 || limit > 200) limit = 50;

            // Whitelist runType to prevent unexpected filter values
            if (runType is not null && runType != "push" && runType != "pull")
                return BadRequest(new { message = "runType must be 'push' or 'pull'." });

            var query = _db.SyncRuns
                .AsNoTracking()
                .Include(r => r.User)
                .AsQueryable();

            if (userId.HasValue)
                query = query.Where(r => r.UserId == userId.Value);

            if (runType is not null)
                query = query.Where(r => r.RunType == runType);

            var runs = await query
                .OrderByDescending(r => r.OccurredAt)
                .Take(limit)
                .Select(r => new
                {
                    r.Id,
                    r.OccurredAt,
                    r.RunType,
                    r.UserId,
                    Username = r.User != null ? r.User.Username : "(unknown)",
                    r.MeasurementCount,
                    r.Status,
                    r.ErrorMessage
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "SyncRunsController: returned {Count} sync run(s).", runs.Count);

            return Ok(new { total = runs.Count, runs });
        }
    }
    ```
  - [ ] 5.2 `UserId = Guid.Empty` pull runs will display `Username = "(unknown)"` — this is acceptable for the pull summary view, since pull is a server-side mass read with no single client identity.
  - [ ] 5.3 Do NOT add pagination (page/pageSize) — `limit` with a hard cap of 200 is sufficient for the diagnostics use-case. jqGrid pagination is not required for this controller (the summary view uses a simple HTML table, not jqGrid — see Task 6).

- [ ] **Task 6: Add Sync Run Summary view to ServerService home page** (AC: #1, #2, #3)
  - [ ] 6.1 Open `ServerService/Views/Home/Index.cshtml`.
  - [ ] 6.2 Add a new `<div class="mt-4">` section **before** the existing Administration section (i.e., at the very top, before the Reset button) with a `<h2>Sync Run Summary</h2>` header. This makes diagnostics the first visible thing:

    ```html
    <div class="mt-4">
        <h2>Sync Run Summary</h2>
        <p>Recent sync push and pull operations recorded by ServerService. Newest first. Auto-loads on page open.</p>

        <div class="mb-2">
            <label for="filterUserId">Filter by User:</label>
            <select id="filterUserId" class="form-select form-select-sm d-inline-block w-auto">
                <option value="">All Users</option>
                <!-- Options populated by JavaScript on load -->
            </select>
            <label for="filterRunType" class="ms-2">Run Type:</label>
            <select id="filterRunType" class="form-select form-select-sm d-inline-block w-auto">
                <option value="">All</option>
                <option value="push">Push</option>
                <option value="pull">Pull</option>
            </select>
            <button class="btn btn-sm btn-outline-secondary ms-2" onclick="loadSyncRuns()">Refresh</button>
        </div>

        <div id="syncRunsResult">
            <p class="text-muted">Loading...</p>
        </div>
    </div>
    ```

  - [ ] 6.3 Add the following JavaScript function inside the existing `<script>` block (alongside `resetServer()`):

    ```javascript
    function loadSyncRuns() {
        const userId = document.getElementById('filterUserId').value;
        const runType = document.getElementById('filterRunType').value;
        let url = '/api/v1/sync-runs?limit=50';
        if (userId) url += '&userId=' + encodeURIComponent(userId);
        if (runType) url += '&runType=' + encodeURIComponent(runType);

        fetch(url)
            .then(r => r.json())
            .then(data => {
                const runs = data.runs || [];
                if (runs.length === 0) {
                    document.getElementById('syncRunsResult').innerHTML =
                        '<p class="text-muted">No sync runs recorded yet. Run a Push or Pull scenario first.</p>';
                    return;
                }
                let html = '<table class="table table-sm table-striped table-bordered">'
                    + '<thead><tr><th>Timestamp (UTC)</th><th>Type</th><th>User</th><th>Count</th><th>Status</th><th>Error</th></tr></thead><tbody>';
                for (const r of runs) {
                    const statusClass = r.status === 'success' ? 'text-success' : 'text-danger fw-bold';
                    const error = r.errorMessage ? '<span class="text-danger">' + escapeHtml(r.errorMessage) + '</span>' : '';
                    html += `<tr>
                        <td>${escapeHtml(r.occurredAt)}</td>
                        <td>${escapeHtml(r.runType)}</td>
                        <td>${escapeHtml(r.username || r.userId)}</td>
                        <td>${r.measurementCount}</td>
                        <td class="${statusClass}">${escapeHtml(r.status)}</td>
                        <td>${error}</td>
                    </tr>`;
                }
                html += '</tbody></table>';
                document.getElementById('syncRunsResult').innerHTML = html;

                // Populate user filter dropdown from data (deduplicate)
                const userSelect = document.getElementById('filterUserId');
                const existingValues = Array.from(userSelect.options).map(o => o.value);
                for (const r of runs) {
                    if (r.userId && r.userId !== '00000000-0000-0000-0000-000000000000' && !existingValues.includes(r.userId)) {
                        const opt = document.createElement('option');
                        opt.value = r.userId;
                        opt.textContent = r.username || r.userId;
                        userSelect.appendChild(opt);
                        existingValues.push(r.userId);
                    }
                }
            })
            .catch(err => {
                document.getElementById('syncRunsResult').innerHTML =
                    '<div class="alert alert-danger">Failed to load sync runs: ' + escapeHtml(err.message || String(err)) + '</div>';
            });
    }

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    // Auto-load on page open
    document.addEventListener('DOMContentLoaded', loadSyncRuns);
    ```

  - [ ] 6.4 XSS GUARD: All user-controlled content from the API response is passed through `escapeHtml()` before being inserted into innerHTML. This is required — do not skip it.
  - [ ] 6.5 Do NOT remove or modify any existing sections (`Administration`, `Edge-Case Scenario`, `Full Scenario Cycle`). The new `Sync Run Summary` section is added first, before `Administration`.

- [ ] **Task 7: Write unit tests for `SyncRunsController`** (AC: #1, #2, #3)
  - [ ] 7.1 Create `MicroservicesSync.Tests/SyncRuns/SyncRunSummaryTests.cs`.
  - [ ] 7.2 Use the same in-memory SQLite test pattern established in `MeasurementPullTests.cs`:
    - Declare a `TestableServerDbContextForSyncRuns` inner class (same `RowVersion` → `ValueGeneratedNever` override pattern as `TestableServerDbContextForPull`).
    - Use `SqliteConnection` kept open + `DbContextOptions<ServerDbContext>` over SQLite.
    - Implement `IDisposable` to close and dispose.
    - Seed `Users` entries with the stable GUIDs from `DatabaseSeeder` (e.g., `00000000-0000-0000-0000-000000000001`).
  - [ ] 7.3 Test `GetRecent_NoRuns_ReturnsEmptyList`:
    - Call `GET /api/v1/sync-runs` with no data seeded.
    - Assert HTTP 200. Assert `data.runs.Count == 0`.
  - [ ] 7.4 Test `GetRecent_AfterTwoPushRuns_ReturnsBothNewestFirst`:
    - Seed two `SyncRun` records with different `OccurredAt` values (older first in insertion order).
    - Call `GET /api/v1/sync-runs`.
    - Assert HTTP 200. Assert 2 runs returned. Assert first returned run has the newer `OccurredAt`.
  - [ ] 7.5 Test `GetRecent_FilterByUserId_ReturnsOnlyThatUser`:
    - Seed 3 `SyncRun` records: 2 for `userId=00000000-0000-0000-0000-000000000001`, 1 for `userId=00000000-0000-0000-0000-000000000002`.
    - Call `GET /api/v1/sync-runs?userId=00000000-0000-0000-0000-000000000001`.
    - Assert 2 runs returned, all with `UserId == user1`.
  - [ ] 7.6 Test `GetRecent_FilterByRunType_ReturnsOnlyThatType`:
    - Seed 2 push runs + 1 pull run.
    - Call `GET /api/v1/sync-runs?runType=push`.
    - Assert 2 runs returned, all with `RunType == "push"`.
  - [ ] 7.7 Test `GetRecent_InvalidRunType_Returns400`:
    - Call `GET /api/v1/sync-runs?runType=invalid`.
    - Assert HTTP 400.
  - [ ] 7.8 Test `GetRecent_FailedRun_IncludesErrorMessage`:
    - Seed 1 `SyncRun` with `Status = "failed"` and `ErrorMessage = "Connection dropped"`.
    - Call `GET /api/v1/sync-runs`.
    - Assert 1 run returned with `status = "failed"` and non-null `errorMessage`.
  - [ ] 7.9 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [ ] 7.10 Run `dotnet test` — all 47 existing tests pass; new tests pass (target: 53 total, 47 + 6 new).

- [ ] **Task 8: Manual Docker smoke test** (AC: #1, #2, #3)
  - [ ] 8.1 Run `docker-compose up --build -d` from `MicroservicesSync/`.
  - [ ] 8.2 Verify service startup via `docker-compose logs serverservice | Select-String -Pattern "migrated|seeded"` — confirm migration is applied (new `SyncRuns` table created).
  - [ ] 8.3 Open ServerService home page — confirm "Sync Run Summary" section appears at the top with "No sync runs recorded yet."
  - [ ] 8.4 From any ClientService UI: click Push Measurements. Return to ServerService home page and click Refresh in the Sync Run Summary section. Confirm at least one push run appears with correct user, count, and status = success.
  - [ ] 8.5 Confirm existing Reset, Edge-Case Scenario, and Repeatable Runs sections still render correctly (no regressions).
  - [ ] 8.6 Run `docker-compose down` and confirm exit code 0.

## Dev Notes

### ⚠️ Epic 3 Story 3.1 — SIZE CHECK PASSED

The Epic 2 retrospective flagged Story 3.1 as a potential "Story 1.4 candidate" (multi-layer spike risk). The scope was evaluated:
- **New domain entity**: `SyncRun` in `Sync.Domain` — 1 file, simple append-only model.
- **New EF configuration**: `SyncRunConfiguration` in `Sync.Infrastructure` — 1 file, no complex mapping.
- **1 new migration**: `AddSyncRunTable` — straightforward table creation.
- **Controller changes**: `SyncMeasurementsController` modified (add SyncRun inserts) + new `SyncRunsController` (read-only summary API).
- **UI change**: Add summary section + JS to existing `Index.cshtml`.
- **Tests**: 6 new unit tests.

**Assessment**: Manageable. No new Application layer service is required. Direct `ServerDbContext` injection is used (consistent with established `// NOTE (Mx)` deviation pattern). Scope guards prevent scope creep into Story 3.2 concerns (jqGrid CRUD for Measurements, structured logging).

### Architecture Constraints (MUST follow)

- **`SyncRun` is ServerService-only**: No ClientService migration, no ClientService DbContext change, no ClientService controller. ClientService doesn't know about sync run summaries.
- **`SyncRun` insert is NOT inside the measurement transaction**: It is a best-effort, separate `SaveChangesAsync` call after the measurement transaction commits (or rolls back). If the `SyncRun` insert itself fails, log the error but do not surface it to the caller — the measurement push already succeeded.
- **No Application layer service for `SyncRun`**: Consistent with the `// NOTE (Mx)` deviation pattern used in `AdminController` (NOTE M4) and `SyncMeasurementsController`. A `SyncRunService` in `Sync.Application` is unnecessary for the experiment scope.
- **No correlation ID yet**: Story 3.4 handles structured logging and correlation IDs. Story 3.1 records basic summary information only (timestamp, user, type, count, status). Do NOT introduce `RunId` or correlation threading in this story.
- **SQL injection prevention**: The `SyncRunsController` uses `runType` query parameter whitelisting (`"push"` or `"pull"` only) to prevent unexpected values from reaching the database query. `userId` is typed as `Guid?` — EF Core handles parameterization automatically. No string interpolation in SQL.
- **XSS prevention**: The JavaScript rendering of sync run data must use `escapeHtml()` for all values sourced from the API response. See Task 6.4.

### What NOT to Do in This Story (Scope Guards)

- ❌ Do NOT add jqGrid endpoints (`GetPaged`, `GetById`, `Create`, `Update`, `Delete`) for `SyncRun` — this is a plain HTML table view, not a jqGrid. Story 3.2 handles jqGrid for Measurements.
- ❌ Do NOT add structured logging or correlation IDs — Story 3.4 owns this.
- ❌ Do NOT modify `ClientService` in any way.
- ❌ Do NOT modify any existing tests (47 tests currently pass; they must continue to pass).
- ❌ Do NOT add a `SyncRun` `DbSet` to `ClientDbContext`.
- ❌ Do NOT change the push/pull batching logic or transaction scope in `SyncMeasurementsController`.
- ❌ Do NOT add authentication or authorization to the new `/api/v1/sync-runs` endpoint.
- ❌ Do NOT add `RowVersion` to `SyncRun` — it is append-only.

### `// NOTE` Deviation Pattern

This story adds one new acknowledged deviation:
```csharp
// NOTE (M3.1): SyncRunsController injects ServerDbContext directly.
// Consistent with the acknowledged deviation in AdminController (NOTE M4) and
// SyncMeasurementsController. Acceptable for this experiment scope.
```
Add this comment to `SyncRunsController`. The pattern is established — do not argue for extracting a `SyncRunService` in `Sync.Application` unless explicitly asked.

### Test Pattern Reference

Use the EXACT same pattern as `TestableServerDbContextForPull` in `MeasurementPullTests.cs`:
```csharp
internal sealed class TestableServerDbContextForSyncRuns : ServerDbContext
{
    public TestableServerDbContextForSyncRuns(DbContextOptions<ServerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entityType.FindProperty("RowVersion");
            if (rowVersionProp != null)
                modelBuilder.Entity(entityType.ClrType)
                    .Property("RowVersion")
                    .ValueGeneratedNever();
        }
    }
}
```
Do NOT reuse the `TestableServerDbContextForPull` class — it is `internal sealed` to the `MeasurementPullTests.cs` context. Declare a new, identically structured class locally in `SyncRunSummaryTests.cs`.

### Migration Command Reference

Run from the solution root:
```powershell
cd "c:\Eugen Files\Projects\BMAD-Microserices-Sync\MicroservicesSync"
dotnet ef migrations add AddSyncRunTable --project Sync.Infrastructure --startup-project ServerService --output-dir Data/Migrations/Server
```
Verify the migration file is created at `Sync.Infrastructure/Data/Migrations/Server/` (same folder as `20260305114117_InitialCreate.cs`).

### Stable GUIDs for Test Seeding

From `DatabaseSeeder.cs` — use these stable GUIDs in test seed helpers to avoid FK violations:
- User 1: `00000000-0000-0000-0000-000000000001` (Username: "user1")
- User 2: `00000000-0000-0000-0000-000000000002` (Username: "user2")

`SyncRun.UserId` must reference a valid `User.Id` to satisfy the FK constraint (even in SQLite in-memory tests).

### `SyncRun` Entity Design Rationale (from Epic 2 Retro PREP-C)

The Epic 2 retrospective (Action Item #3) specifically called out an architecture decision needed for Story 3.1:
> "Pre-story design decision on Story 3.1: SyncRun table vs derived summary — architecture decision documented before create-story."

**Decision taken**: Dedicated `SyncRun` entity (not derived from `Measurements`).

**Rationale**:
- Deriving summaries from `Measurements` table aggregates (`GROUP BY UserId + RecordedAt`) cannot capture failed runs (which produce no `Measurement` rows), nor can it reliably distinguish separate push runs from the same user if they happen within the same second.
- A dedicated, append-only `SyncRun` table is minimal (7 columns), has a well-defined scope (one row per push/pull invocation), and cleanly supports the AC requirements for failure status and filtering.
- The data model stays simple: `SyncRun` has a single FK to `Users` — no FK to `Measurements`.

### Project Structure Notes

**Files to create:**
- `Sync.Domain/Entities/SyncRun.cs` — new append-only entity
- `Sync.Infrastructure/Data/Configurations/SyncRunConfiguration.cs` — EF configuration
- `Sync.Infrastructure/Data/Migrations/Server/<timestamp>_AddSyncRunTable.cs` — generated migration
- `Sync.Infrastructure/Data/Migrations/Server/<timestamp>_AddSyncRunTable.Designer.cs` — generated
- `ServerService/Controllers/SyncRunsController.cs` — read-only summary API
- `MicroservicesSync.Tests/SyncRuns/SyncRunSummaryTests.cs` — 6 unit tests

**Files to modify:**
- `Sync.Infrastructure/Data/ServerDbContext.cs` — add `DbSet<SyncRun>`, `ApplyConfiguration(new SyncRunConfiguration())`
- `ServerService/Controllers/SyncMeasurementsController.cs` — insert `SyncRun` records on push success/failure and pull success/failure
- `ServerService/Views/Home/Index.cshtml` — add Sync Run Summary section at the top

**Files to NOT modify:**
- `Sync.Infrastructure/Data/ClientDbContext.cs` — no SyncRun on client
- `ClientService/**` — no changes to ClientService
- `Sync.Domain/Entities/Measurement.cs` and all other existing entities
- Any existing test file in `MicroservicesSync.Tests/`
- `docker-compose.yml` and `docker-compose.override.yml` — no configuration changes needed
- `ServerService/Program.cs` — EF Core applies migrations automatically at startup; no changes needed

### References

- Story requirement (AC#1, AC#2, AC#3): [Source: `_bmad-output/planning-artifacts/epics.md#Story 3.1`]
- FR10 (sync run summary): [Source: `_bmad-output/planning-artifacts/prd.md`]
- Architecture: diagnostics observability, logging/diagnostics cross-cutting concern: [Source: `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns Identified`]
- Architecture: SQL injection prevention (whitelist approach for string params): [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication & Security`]
- Acknowledged deviation pattern `// NOTE (Mx)`: [Source: `_bmad-output/implementation-artifacts/2-2-transactional-batched-server-side-measurement-push.md`] and `ServerService/Controllers/AdminController.cs`
- Test pattern (`TestableServerDbContext`, `SqliteConnection`, `IDisposable`): [Source: `MicroservicesSync/MicroservicesSync.Tests/Measurements/MeasurementPullTests.cs`]
- Stable seeded GUIDs: [Source: `MicroservicesSync/Sync.Infrastructure/Data/DatabaseSeeder.cs`]
- Existing `SyncMeasurementsController` (push/pull/count actions): [Source: `MicroservicesSync/ServerService/Controllers/SyncMeasurementsController.cs`]
- Existing `ServerDbContext` (how ApplyConfiguration and RowVersion foreach work): [Source: `MicroservicesSync/Sync.Infrastructure/Data/ServerDbContext.cs`]
- `SyncRun` design decision (PREP-C): [Source: `_bmad-output/implementation-artifacts/epic-2-retro-2026-03-09.md#Action Items`]
- Migration location (existing migrations in `Data/Migrations/Server/`): [Source: `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Server/`]
- ServerService home page (existing sections to preserve): [Source: `MicroservicesSync/ServerService/Views/Home/Index.cshtml`]
- Project Context (layering rules, EF Core patterns, security): [Source: `_bmad-output/project-context.md`]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

### Completion Notes List

### File List
