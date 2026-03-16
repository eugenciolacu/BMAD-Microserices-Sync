# Story 5.1: Update Reset to Baseline Completeness

Status: done

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

- [x] **Task 1: Update `ResetServerAsync` in `Sync.Infrastructure/Data/DatabaseResetter.cs`** (AC: #1)
  - [x] 1.1 Locate the `ResetServerAsync` method bulk-delete sequence.
  - [x] 1.2 Add `await db.SyncRuns.ExecuteDeleteAsync(cancellationToken);` **after** `db.Measurements.ExecuteDeleteAsync` and **before** `db.Cells.ExecuteDeleteAsync`. The correct FK-safe sequence becomes:
    1. `db.Measurements.ExecuteDeleteAsync(cancellationToken)`
    2. `db.SyncRuns.ExecuteDeleteAsync(cancellationToken)` ← **NEW**
    3. `db.Cells.ExecuteDeleteAsync(cancellationToken)`
    4. `db.Surfaces.ExecuteDeleteAsync(cancellationToken)`
    5. `db.Rooms.ExecuteDeleteAsync(cancellationToken)`
    6. `db.Buildings.ExecuteDeleteAsync(cancellationToken)`
    7. `db.Users.ExecuteDeleteAsync(cancellationToken)`

    **Rationale:** `SyncRun.UserId` is a nullable FK to `Users.Id`. Deleting `SyncRuns` before `Users` satisfies the FK constraint. It has no FK to Cells/Surfaces/Rooms/Buildings so its exact position relative to those is flexible; placing it immediately after Measurements keeps the sequence logically grouped (transactional data first, then reference hierarchy).
  - [x] 1.3 Update the XML doc comment on `ResetServerAsync` to add `SyncRuns` to the "After completion:" line:
    ```
    /// After completion: 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells, 0 Measurements, 0 SyncRuns.
    ```

- [x] **Task 2: Update unit tests in `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs`** (AC: #1)
  - [x] 2.1 In the private helper `SeedServerWithMeasurementsAsync(ServerDbContext db)`, add a `SyncRun` record **after** the existing `db.Measurements.AddRangeAsync(...)` call and before `await db.SaveChangesAsync()`:
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
  - [x] 2.2 Add `using Sync.Domain.Entities;` to the file's using block if it is not already present (it should already exist via `Measurement`).
  - [x] 2.3 In the test method `ResetServerAsync_DeletesMeasurements_AndReseeds_ReferenceData`:
    - **Before Act:** Add `Assert.Equal(1, await _serverDb.SyncRuns.CountAsync());` to confirm the seeded SyncRun is present.
    - **After Assert block:** Add `Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());`
  - [x] 2.4 In the test method `ResetServerAsync_Is_Idempotent`:
    - After the two-reset sequence, add `Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());`
  - [x] 2.5 In the test method `ResetServerAsync_OnEmptyDatabase_SeedsReferenceData`:
    - After the Act + Assert block, add `Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());`
    - Note: this test starts from an empty database — `SyncRuns` count of 0 validates no phantom rows appear during seed.

## Dev Notes

- `ResetClientAsync` requires no changes. `ClientDbContext` does not have a `SyncRuns` DbSet.
- The UI buttons ("Reset to Baseline" on ServerService and "Reset Client DB" on ClientService) do not need changes — they already POST to `api/v1/admin/reset` which calls the corrected `DatabaseResetter` methods.
- No new migrations are required. This change modifies only the data-cleanup logic.
- **Pattern note:** If any future story adds a new table to `ServerDbContext` (e.g., `AuditLog`, `SyncBatch`), the developer must add a corresponding `ExecuteDeleteAsync` in the correct FK-safe position in `DatabaseResetter.ResetServerAsync` within that same story's tasks.

## Dev Agent Record

### Implementation Plan
Added `db.SyncRuns.ExecuteDeleteAsync(cancellationToken)` to `ResetServerAsync` immediately after `Measurements` and before `Cells`. This position satisfies the FK constraint (`SyncRun.UserId → Users.Id`) and logically groups transactional data (Measurements + SyncRuns) before the reference hierarchy. Updated the XML doc comment and class-level summary comment to reflect the new deletion order. Tests were expanded to seed a `SyncRun` and assert it is cleared by the reset operation.

### Completion Notes
- ✅ Task 1: `DatabaseResetter.ResetServerAsync` updated — `SyncRuns` deleted after `Measurements`, before `Cells`. XML doc and class summary updated.
- ✅ Task 2: `DatabaseResetterTests` updated — `SeedServerWithMeasurementsAsync` seeds 1 SyncRun; three test methods assert `SyncRuns.CountAsync() == 0` after reset; `ResetServerAsync_DeletesMeasurements_AndReseeds_ReferenceData` asserts count of 1 before Act.
- ✅ All 6 Reset tests pass. Pre-existing 5 failures in MeasurementPush/PullTests (NullReferenceException on `Request.Headers`) are unrelated to this story.
- ✅ No new migrations required. No UI changes required. `ResetClientAsync` unchanged (no `SyncRuns` DbSet on ClientDbContext).

## File List

- `MicroservicesSync/Sync.Infrastructure/Data/DatabaseResetter.cs` — modified
- `MicroservicesSync/MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs` — modified
- `MicroservicesSync/Sync.Domain/Entities/SyncRun.cs` — modified (RowVersion added)

## Senior Developer Review (AI)

**Reviewer:** Eugen on 2026-03-16
**Verdict:** Changes Requested → Fixed

### Findings

| Severity | Finding | Location | Resolution |
|----------|---------|----------|------------|
| HIGH | `SyncRun` entity missing `RowVersion` concurrency token — every other SQL Server entity has it; project-context rule mandates it | `SyncRun.cs` | Fixed: added `public byte[] RowVersion { get; set; } = Array.Empty<byte>();`. Picked up automatically by `ServerDbContext.OnModelCreating` rowversion loop. |
| MEDIUM | `ResetServerAsync_Is_Idempotent` missing pre-Act `SyncRuns.CountAsync()` assertion — post-reset `== 0` was vacuously passable if seeding was silently broken | `DatabaseResetterTests.cs` | Fixed: added `Assert.Equal(1, await _serverDb.SyncRuns.CountAsync());` before Act. |
| LOW | `ResetClientAsync_Is_Idempotent` and `ResetClientAsync_OnEmptyDatabase_RemainsEmpty` assert only Measurements + Users (pre-existing from Story 1.5, not introduced here) | `DatabaseResetterTests.cs` | Noted; no change — out of scope for this story. |

**Post-fix:** All 6 Reset tests pass.

## Change Log

- 2026-03-16: Added `SyncRuns` to FK-safe delete sequence in `DatabaseResetter.ResetServerAsync`; updated tests to seed and assert SyncRun deletion (Story 5.1).
- 2026-03-16: Code review fixes — added `RowVersion` concurrency token to `SyncRun` entity (HIGH); strengthened `ResetServerAsync_Is_Idempotent` with pre-Act SyncRun count assertion (MEDIUM).
