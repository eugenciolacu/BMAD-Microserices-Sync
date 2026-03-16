# Story 5.1: Update Reset to Baseline Completeness

Status: ready-for-dev

## Story

As a developer,
I want the "Reset to Baseline" button on ServerService and the "Reset Client DB" button on ClientService to fully clear all data from their respective databases,
so that the reset operation leaves no stale data behind regardless of which tables were added after the initial reset implementation.

## Background

Story 1.5 implemented `DatabaseResetter.ResetServerAsync` with a delete sequence of:
`Measurements → Cells → Surfaces → Rooms → Buildings → Users`

Story 3.1 later introduced the `SyncRun` entity (`Sync.Domain/Entities/SyncRun.cs`) and added `DbSet<SyncRun> SyncRuns` to `ServerDbContext`. The `DatabaseResetter` was not updated at that time, so clicking "Reset to Baseline" (ServerService) leaves all accumulated `SyncRuns` records in the database.

`ClientDbContext` does not expose `SyncRuns` — no changes needed to `ResetClientAsync` or to the ClientService "Reset Client DB" button behavior.

## Acceptance Criteria

1. **Given** the system has accumulated sync history (SyncRuns) and measurement data  
   **When** I click "Reset to Baseline" on ServerService  
   **Then** the `SyncRuns` table is fully cleared along with all other tables, and reference data is re-seeded, leaving zero orphaned records in any table.

2. **Given** a ClientService instance has accumulated data  
   **When** I click "Reset Client DB" on that ClientService instance  
   **Then** all tables are fully cleared (no change to existing ClientService behavior — ClientService has no SyncRuns table).

3. **Given** a future story adds a new table to `ServerDbContext` or `ClientDbContext`  
   **When** the developer implements the new entity  
   **Then** the same story must include an update to `DatabaseResetter` adding the new table to the FK-safe deletion order.

## Tasks / Subtasks

- [ ] **Task 1: Update `ResetServerAsync` in `Sync.Infrastructure/Data/DatabaseResetter.cs`** (AC: #1)
  - [ ] 1.1 Locate the `ResetServerAsync` method bulk-delete sequence.
  - [ ] 1.2 Add `await db.SyncRuns.ExecuteDeleteAsync(cancellationToken);` **after** `db.Measurements.ExecuteDeleteAsync` and **before** `db.Cells.ExecuteDeleteAsync`. The correct FK-safe sequence becomes:
    1. `db.Measurements.ExecuteDeleteAsync(cancellationToken)`
    2. `db.SyncRuns.ExecuteDeleteAsync(cancellationToken)` ← **NEW**
    3. `db.Cells.ExecuteDeleteAsync(cancellationToken)`
    4. `db.Surfaces.ExecuteDeleteAsync(cancellationToken)`
    5. `db.Rooms.ExecuteDeleteAsync(cancellationToken)`
    6. `db.Buildings.ExecuteDeleteAsync(cancellationToken)`
    7. `db.Users.ExecuteDeleteAsync(cancellationToken)`

    **Rationale:** `SyncRun.UserId` is a nullable FK to `Users.Id`. Deleting `SyncRuns` before `Users` satisfies the FK constraint. It has no FK to Cells/Surfaces/Rooms/Buildings so its exact position relative to those is flexible; placing it immediately after Measurements keeps the sequence logically grouped (transactional data first, then reference hierarchy).
  - [ ] 1.3 Update the XML doc comment on `ResetServerAsync` to add `SyncRuns` to the "After completion:" line:
    ```
    /// After completion: 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells, 0 Measurements, 0 SyncRuns.
    ```

- [ ] **Task 2: Update unit tests in `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs`** (AC: #1)
  - [ ] 2.1 In the private helper `SeedServerWithMeasurementsAsync(ServerDbContext db)`, add a `SyncRun` record **after** the existing `db.Measurements.AddRangeAsync(...)` call and before `await db.SaveChangesAsync()`:
    ```csharp
    await db.SyncRuns.AddRangeAsync(new[]
    {
        new SyncRun
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            RunType = "push",
            UserId = userId,
            MeasurementCount = 2,
            Status = "success"
        }
    });
    ```
    This ensures the reset tests verify that a pre-existing SyncRun is deleted.
  - [ ] 2.2 Add `using Sync.Domain.Entities;` to the file's using block if it is not already present (it should already exist via `Measurement`).
  - [ ] 2.3 In the test method `ResetServerAsync_DeletesMeasurements_AndReseeds_ReferenceData`:
    - **Before Act:** Add `Assert.Equal(1, await _serverDb.SyncRuns.CountAsync());` to confirm the seeded SyncRun is present.
    - **After Assert block:** Add `Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());`
  - [ ] 2.4 In the test method `ResetServerAsync_Is_Idempotent`:
    - After the two-reset sequence, add `Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());`
  - [ ] 2.5 In the test method `ResetServerAsync_OnEmptyDatabase_SeedsReferenceData`:
    - After the Act + Assert block, add `Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());`
    - Note: this test starts from an empty database — `SyncRuns` count of 0 validates no phantom rows appear during seed.

## Dev Notes

- `ResetClientAsync` requires no changes. `ClientDbContext` does not have a `SyncRuns` DbSet.
- The UI buttons ("Reset to Baseline" on ServerService and "Reset Client DB" on ClientService) do not need changes — they already POST to `api/v1/admin/reset` which calls the corrected `DatabaseResetter` methods.
- No new migrations are required. This change modifies only the data-cleanup logic.
- **Pattern note:** If any future story adds a new table to `ServerDbContext` (e.g., `AuditLog`, `SyncBatch`), the developer must add a corresponding `ExecuteDeleteAsync` in the correct FK-safe position in `DatabaseResetter.ResetServerAsync` within that same story's tasks.
