# Story 1.5: Reset Environment to Clean Baseline

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a documented way to reset all databases to the clean baseline,
so that I can repeat experiments from the same starting point.

## Acceptance Criteria

1. **Given** the system has been used and data has changed  
   **When** I execute the documented "clean & seed" reset (e.g., HTTP POST to `/api/v1/admin/reset` on ServerService plus `/api/v1/admin/reset` on each ClientService)  
   **Then** ServerService's SQL Server database is returned to the reference-data-only state: all Measurements are deleted, and all reference tables (Users, Buildings, Rooms, Surfaces, Cells) are deleted and re-seeded with the same stable GUIDs as the original seed.

2. **Given** the reset has finished  
   **When** each ClientService restarts (or startup logic re-runs), and the reference-data auto-pull detects an empty local DB  
   **Then** all ClientService SQLite databases are re-populated with the same reference data set — matching the fresh-install state with zero Measurements.

3. **Given** the reset operation is documented  
   **When** a new developer follows the README reset steps  
   **Then** they can reliably restore the environment to the clean baseline without manual database operations (no SQL drops, no container recreation).

## Tasks / Subtasks

- [ ] **Task 1: Create `DatabaseResetter.cs` in `Sync.Infrastructure/Data/`** (AC: #1, #2)
  - [ ] 1.1 Create `Sync.Infrastructure/Data/DatabaseResetter.cs` as a `public static class DatabaseResetter`.
  - [ ] 1.2 Implement `public static async Task ResetServerAsync(ServerDbContext db)`:
    - Open an explicit transaction (`await db.Database.BeginTransactionAsync()`).
    - Delete all rows from each table in FK-safe reverse order using EF Core 10 `ExecuteDeleteAsync()`:
      1. `db.Measurements.ExecuteDeleteAsync()`
      2. `db.Cells.ExecuteDeleteAsync()`
      3. `db.Surfaces.ExecuteDeleteAsync()`
      4. `db.Rooms.ExecuteDeleteAsync()`
      5. `db.Buildings.ExecuteDeleteAsync()`
      6. `db.Users.ExecuteDeleteAsync()`
    - Re-seed all reference tables by calling `await DatabaseSeeder.SeedAsync(db)` **after** clearing (SeedAsync guard will NOT block because tables are now empty).
    - Commit the transaction.
  - [ ] 1.3 Implement `public static async Task ResetClientAsync(ClientDbContext db)`:
    - Open an explicit transaction.
    - Delete all rows in FK-safe reverse order using `ExecuteDeleteAsync()`:
      1. `db.Measurements.ExecuteDeleteAsync()`
      2. `db.Cells.ExecuteDeleteAsync()`
      3. `db.Surfaces.ExecuteDeleteAsync()`
      4. `db.Rooms.ExecuteDeleteAsync()`
      5. `db.Buildings.ExecuteDeleteAsync()`
      6. `db.Users.ExecuteDeleteAsync()`
    - Commit the transaction.
    - Do NOT re-seed ClientService — startup logic auto-pulls reference data from ServerService when local DB is empty.

- [ ] **Task 2: Add `POST /api/v1/admin/reset` endpoint to ServerService** (AC: #1)
  - [ ] 2.1 Create `ServerService/Controllers/AdminController.cs` as `[ApiController, Route("api/v1/admin")]`.
  - [ ] 2.2 Inject `ServerDbContext` via constructor, and `ILogger<AdminController>`.
  - [ ] 2.3 Add `[HttpPost("reset")]` action that:
    - Logs `"ServerService: reset initiated."`.
    - Calls `await DatabaseResetter.ResetServerAsync(_db)`.
    - Logs `"ServerService: reset complete. Reference data re-seeded."`.
    - Returns `Ok(new { message = "ServerService reset complete." })`.
  - [ ] 2.4 Wrap in try/catch; return `StatusCode(500, ...)` on exception with logged error.

- [ ] **Task 3: Add `POST /api/v1/admin/reset` endpoint to ClientService** (AC: #2)
  - [ ] 3.1 Create `ClientService/Controllers/AdminController.cs` as `[ApiController, Route("api/v1/admin")]`.
  - [ ] 3.2 Inject `ClientDbContext` via constructor, and `ILogger<AdminController>`.
  - [ ] 3.3 Add `[HttpPost("reset")]` action that:
    - Logs `"ClientService: reset initiated."`.
    - Calls `await DatabaseResetter.ResetClientAsync(_db)`.
    - Logs `"ClientService: reset complete. DB cleared — reference data will be pulled on next startup trigger."`.
    - Returns `Ok(new { message = "ClientService reset complete. Restart or trigger reference pull to reload reference data." })`.
  - [ ] 3.4 Wrap in try/catch; return `StatusCode(500, ...)` on exception with logged error.

- [ ] **Task 4: Add "Reset" UI action to Home pages** (AC: #1, #2, #3)
  - [ ] 4.1 In `ServerService/Views/Home/Index.cshtml`, add a "Reset to Baseline" button that POSTs to `/api/v1/admin/reset` using a small JavaScript `fetch` call (or Razor form POST). Display a confirmation and result toast/alert.
  - [ ] 4.2 In `ClientService/Views/Home/Index.cshtml`, add a similar "Reset Client DB" button that POSTs to `/api/v1/admin/reset`. Display a confirmation/result alert.
  - [ ] 4.3 Both buttons should include a JS `confirm("Are you sure? This will delete all measurements.")` guard before submitting.
  - [ ] 4.4 After a successful ClientService reset, the UI should hint that the user must restart the ClientService container (or trigger the reference-data pull) to repopulate reference data.

- [ ] **Task 5: Add reference-data re-pull trigger endpoint to ClientService** (AC: #2)
  - [ ] 5.1 Add `[HttpPost("pull-reference-data")]` action to `ClientService/Controllers/AdminController.cs`.
  - [ ] 5.2 Reuse the same inline reference-pull logic from `Program.cs` (extract into a new `ReferenceDataLoader` static helper in `Sync.Infrastructure/Data/` or `ClientService` to avoid duplication):
    - Check `if (!await db.Users.AnyAsync())` then perform HTTP GET to `api/v1/sync/reference-data` and insert all entities.
  - [ ] 5.3 Return `Ok(new { message = "Reference data loaded." })` or `BadRequest(new { message = "Reference data already present." })` if the DB is not empty (idempotency guard).
  - [ ] 5.4 Add a "Pull Reference Data" button to `ClientService/Views/Home/Index.cshtml` that calls this endpoint (visible only or greyed-out note after reset).

- [ ] **Task 6: Document reset flow** (AC: #3)
  - [ ] 6.1 Add a **"Reset to Clean Baseline"** section to `MicroservicesSync/README.md` (create README.md if it only has a stub):
    - Prerequisites: all containers running (`docker-compose up`).
    - Step 1 — Reset ServerService: `curl -X POST http://localhost:5000/api/v1/admin/reset`
    - Step 2 — Reset each ClientService (repeat for ports 5001–5005): `curl -X POST http://localhost:5001/api/v1/admin/reset`
    - Step 3 — Reload reference data on each ClientService (or restart containers): `curl -X POST http://localhost:5001/api/v1/admin/pull-reference-data`
    - Alternatively: use the "Reset to Baseline" / "Pull Reference Data" buttons on each service's home page.
    - Expected result: ServerService has 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells, 0 Measurements. Each ClientService has the same reference data and 0 Measurements.

- [ ] **Task 7: Write tests** (AC: #1, #2)
  - [ ] 7.1 Add `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs`:
    - Using an in-memory or SQLite-backed `ServerDbContext` / `ClientDbContext` (consistent with how existing tests are set up — check `MicroservicesSync.Tests.csproj` for existing packages).
    - Test `ResetServerAsync`: seed a db with reference data + fake measurements → call reset → assert all Measurements gone, all reference data re-seeded with correct counts (5 users, 2 buildings, 4 rooms, 8 surfaces, 16 cells, 0 measurements).
    - Test `ResetClientAsync`: populate a ClientDbContext with reference data + measurements → call reset → assert all tables empty.
    - Test idempotency: call reset twice → should produce same clean state.
  - [ ] 7.2 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [ ] 7.3 Run `dotnet test` — all existing tests (13+) pass, new reset tests pass.
  - [ ] 7.4 Verify manually (Docker): run `docker-compose up --build`, generate some data, call reset endpoints, confirm baseline via SSMS and SQLite viewer.

## Dev Notes

### Architecture Constraints (MUST follow)

- **All DB-reset logic belongs in `Sync.Infrastructure/Data/`**, not in controllers. Create `DatabaseResetter.cs` there. Controllers only call it — they do not contain SQL or EF Core logic directly.
- **Use `ExecuteDeleteAsync()` (EF Core 7+), NOT `RemoveRange` + `SaveChangesAsync`**. `ExecuteDeleteAsync()` translates to a single `DELETE FROM` SQL statement without loading entities into memory — required for performance and correctness at scale. EF Core 10.0.3 is confirmed installed in this project. Do NOT use `_db.Measurements.RemoveRange(await _db.Measurements.ToListAsync())` — this loads all rows into memory and is O(n).
- **FK deletion order is critical**. SQL Server will reject FK-violating deletes. Always delete in this exact order:
  1. Measurements (references Users and Cells)
  2. Cells (references Surfaces)
  3. Surfaces (references Rooms)
  4. Rooms (references Buildings)
  5. Buildings (no FK)
  6. Users (no FK — but Measurements FK on Users must be gone first)
- **`DatabaseSeeder.SeedAsync` is reused** for the re-seed step. The idempotency guard (`if (await db.Users.AnyAsync()) return;`) in `DatabaseSeeder.SeedAsync` will **not** block after a full clear because the tables will be empty. Do **not** modify `DatabaseSeeder.SeedAsync` — calling it after `ExecuteDeleteAsync` on all tables is the correct, non-duplicating approach.
- **ClientService does NOT re-seed itself**. After `ResetClientAsync`, the DB is fully empty. The existing startup auto-pull logic in `Program.cs` (the `if (!await db.Users.AnyAsync())` block) handles reference-data repopulation. Expose `POST /api/v1/admin/pull-reference-data` to allow triggering this without a container restart.
- **No new NuGet packages are needed**. `Microsoft.EntityFrameworkCore` 10.0.3 is already installed in `Sync.Infrastructure.csproj`. `ExecuteDeleteAsync()` is included.
- **Transactions wrap the full reset**, ensuring all-or-nothing behavior — if any delete fails, none of the changes are committed.
- **Do NOT drop and recreate the database** (no `db.Database.EnsureDeletedAsync()` or dropping migrations). The reset is purely a data operation, not a schema operation. Migrations remain intact.
- **Clean/onion layer compliance**: `Sync.Infrastructure` → referenced by `ServerService` and `ClientService`. The new `DatabaseResetter` class in `Sync.Infrastructure` is consistent with where `DatabaseSeeder` already lives.

### Key Implementation Pattern

#### `DatabaseResetter.cs` (complete structure)

```csharp
using Microsoft.EntityFrameworkCore;

namespace Sync.Infrastructure.Data;

/// <summary>
/// Resets ServerService and ClientService databases to clean baseline state.
/// Uses EF Core ExecuteDeleteAsync for bulk deletes (no entity loading).
/// FK-safe deletion order is enforced: Measurements → Cells → Surfaces → Rooms → Buildings → Users.
/// </summary>
public static class DatabaseResetter
{
    /// <summary>
    /// Clears all data from ServerService SQL Server database and re-seeds reference data.
    /// After completion: 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells, 0 Measurements.
    /// </summary>
    public static async Task ResetServerAsync(ServerDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // FK-safe bulk deletes (no entity loading — O(1) SQL DELETE FROM statements)
        await db.Measurements.ExecuteDeleteAsync();
        await db.Cells.ExecuteDeleteAsync();
        await db.Surfaces.ExecuteDeleteAsync();
        await db.Rooms.ExecuteDeleteAsync();
        await db.Buildings.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();

        await tx.CommitAsync();

        // Re-seed reference data (SeedAsync guard now succeeds because tables are empty)
        await DatabaseSeeder.SeedAsync(db);
    }

    /// <summary>
    /// Clears ALL data from a ClientService SQLite database (reference data + measurements).
    /// After completion: all tables empty. Reference data is repopulated via startup auto-pull
    /// or POST /api/v1/admin/pull-reference-data.
    /// </summary>
    public static async Task ResetClientAsync(ClientDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Measurements.ExecuteDeleteAsync();
        await db.Cells.ExecuteDeleteAsync();
        await db.Surfaces.ExecuteDeleteAsync();
        await db.Rooms.ExecuteDeleteAsync();
        await db.Buildings.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();

        await tx.CommitAsync();
    }
}
```

#### `AdminController.cs` pattern (same structure on both services, different DbContext type)

```csharp
using Microsoft.AspNetCore.Mvc;
using Sync.Infrastructure.Data;

namespace ServerService.Controllers; // or ClientService.Controllers

[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly ServerDbContext _db; // ClientDbContext for ClientService
    private readonly ILogger<AdminController> _logger;

    public AdminController(ServerDbContext db, ILogger<AdminController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        try
        {
            _logger.LogInformation("ServerService: reset initiated.");
            await DatabaseResetter.ResetServerAsync(_db); // ResetClientAsync for ClientService
            _logger.LogInformation("ServerService: reset complete.");
            return Ok(new { message = "ServerService reset complete." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerService: reset failed.");
            return StatusCode(500, new { message = "Reset failed.", error = ex.Message });
        }
    }
}
```

### SQLite `ExecuteDeleteAsync` Note

SQLite supports `DELETE FROM` so `ExecuteDeleteAsync()` works correctly with `ClientDbContext`. No provider-specific workaround needed. Confirmed: EF Core SQLite provider 10.0.3 supports bulk `ExecuteDeleteAsync`.

### Reference-data Pull Re-use (ClientService AdminController)

The reference-data pull logic currently lives inline in `ClientService/Program.cs`. To avoid duplication, consider extracting it to a private static method or a simple helper class. However, if the developer prefers minimal change, it is acceptable to duplicate the logic in the `pull-reference-data` action for this story since it is a small, self-contained block. Document the duplication with a `// TODO: extract to ReferenceDataLoader` comment for future cleanup.

### Files to Create / Modify

| Action | Path |
|--------|------|
| CREATE | `Sync.Infrastructure/Data/DatabaseResetter.cs` |
| CREATE | `ServerService/Controllers/AdminController.cs` |
| CREATE | `ClientService/Controllers/AdminController.cs` |
| MODIFY | `ServerService/Views/Home/Index.cshtml` — add Reset button |
| MODIFY | `ClientService/Views/Home/Index.cshtml` — add Reset + Pull Reference Data buttons |
| MODIFY | `MicroservicesSync/README.md` — add Reset to Clean Baseline section |
| CREATE | `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs` |

### Files NOT to Touch

- `Sync.Infrastructure/Data/DatabaseSeeder.cs` — reused as-is; do NOT modify
- `ServerService/Program.cs` — no changes needed
- `ClientService/Program.cs` — no changes needed (auto-pull logic stays intact)
- `docker-compose.yml` — no changes needed
- All EF Core migrations — no schema changes, data-only reset
- `Sync.Domain/` entities — no changes needed

### Previous Story Learnings (from Story 1.4)

- **EF Core package versions confirmed**: `Microsoft.EntityFrameworkCore` 10.0.3, `Microsoft.EntityFrameworkCore.SqlServer` 10.0.3, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.3 — all already installed. No new packages needed.
- **`Sync.Infrastructure` has `ServerDbContext` and `ClientDbContext`** — both fully functional with all 6 `DbSet<T>` properties: `Users`, `Buildings`, `Rooms`, `Surfaces`, `Cells`, `Measurements`.
- **Stable seed GUIDs** (used in `DatabaseSeeder.SeedAsync` — reused by `DatabaseResetter.ResetServerAsync` via `SeedAsync` call):
  - Users: `00000000-0000-0000-0000-00000000000{1-5}`
  - Buildings: `10000000-0000-0000-0000-00000000000{1-2}`
  - Rooms: `20000000-0000-0000-0000-00000000000{1-4}`
  - Surfaces: `30000000-0000-0000-0000-00000000000{1-8}`
  - Cells: `40000000-0000-0000-0000-00000000000{1-16}`
- **`SyncReferenceDataController`** already exists at `ServerService/Controllers/SyncReferenceDataController.cs` with `GET /api/v1/sync/reference-data`. The ClientService admin pull-reference-data endpoint should call this same URL.
- **Decimal precision** for `Measurement.Value` is `(18,4)` — not relevant to this story but noted for FK-delete context.
- **Test runner**: `dotnet test` with 13 existing tests passing. New tests go in `MicroservicesSync.Tests/`. Check `.csproj` for test framework (likely xUnit from pattern).
- **Build verification command**: `dotnet build MicrosericesSync.sln` from the `MicroservicesSync/` folder.
- **Docker ports**: ServerService=5000, ClientService User1-5 = 5001-5005.

### Project Structure Notes

- `Sync.Infrastructure/Data/` — existing location of `DatabaseSeeder.cs`; new `DatabaseResetter.cs` goes here.
- `ServerService/Controllers/` — currently has `HomeController.cs` and `SyncReferenceDataController.cs`; new `AdminController.cs` goes here.
- `ClientService/Controllers/` — check existing controllers; new `AdminController.cs` goes here.
- All new files follow existing namespace convention: `Sync.Infrastructure.Data`, `ServerService.Controllers`, `ClientService.Controllers`.

### References

- Story 1.5 acceptance criteria: [_bmad-output/planning-artifacts/epics.md](_bmad-output/planning-artifacts/epics.md) — Epic 1, Story 1.5
- Seeding implementation: [MicroservicesSync/Sync.Infrastructure/Data/DatabaseSeeder.cs](MicroservicesSync/Sync.Infrastructure/Data/DatabaseSeeder.cs)
- ServerDbContext: [MicroservicesSync/Sync.Infrastructure/Data/ServerDbContext.cs](MicroservicesSync/Sync.Infrastructure/Data/ServerDbContext.cs)
- ClientDbContext: [MicroservicesSync/Sync.Infrastructure/Data/ClientDbContext.cs](MicroservicesSync/Sync.Infrastructure/Data/ClientDbContext.cs)
- Reference data endpoint: [MicroservicesSync/ServerService/Controllers/SyncReferenceDataController.cs](MicroservicesSync/ServerService/Controllers/SyncReferenceDataController.cs)
- Project context rules: [_bmad-output/project-context.md](_bmad-output/project-context.md)
- Architecture: [_bmad-output/planning-artifacts/architecture.md](_bmad-output/planning-artifacts/architecture.md)
- Additional requirements (clean/seed spec): [_bmad-output/planning-artifacts/epics.md](_bmad-output/planning-artifacts/epics.md) — Additional Requirements section
- EF Core `ExecuteDeleteAsync` docs: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#executeupdate-and-executedelete-bulk-updates

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.

### File List
