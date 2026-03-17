# Story 5.3: Idempotent Push

Status: done

## Story

As a developer,
I want the ServerService push endpoint to gracefully handle re-pushed measurements whose IDs already exist in the database,
so that a retry after a partial failure (e.g., server committed but client crashed before marking SyncedAt) does not result in an HTTP 500 and a broken sync state.

## Background

The push flow is:
1. ClientService `MeasurementSyncService.PushAsync()` sends all unsynced measurements (`SyncedAt == null`) to ServerService.
2. ServerService inserts them inside a transaction and returns HTTP 200.
3. ClientService marks the measurements as synced (`SyncedAt = now`).

If the service is interrupted between steps 2 and 3, **the same measurement IDs will be re-sent on the next push**. The original implementation used `AddRangeAsync` unconditionally, causing a primary-key conflict → `DbUpdateException` → HTTP 500 → `InvalidOperationException` thrown on the client.

Additionally, `Request.Headers["X-Correlation-Id"]` in the controller caused a `NullReferenceException` in unit-test context where no `HttpContext` is wired up. This was also fixed.

## Acceptance Criteria

1. **Given** a push has already been successfully committed on the server  
   **When** the same measurement IDs are pushed again (e.g., after a client-side crash before `SyncedAt` was set)  
   **Then** the server returns HTTP 200 and skips the duplicate IDs without error — the total measurement count in the database remains unchanged.

2. **Given** a request contains a mix of new and already-existing measurement IDs  
   **When** the push is processed  
   **Then** only the new IDs are inserted; duplicates are silently skipped; the response `Pushed` count reflects only newly inserted records.

3. **Given** duplicates are skipped  
   **When** the server logs the operation  
   **Then** a `LogWarning` entry is emitted indicating how many duplicate IDs were skipped per batch, so operators can diagnose unexpected retry patterns.

4. **Given** the controller is instantiated in a unit-test context (no `HttpContext`)  
   **When** the push action is invoked  
   **Then** no `NullReferenceException` occurs — `Request` access is null-guarded.

## Tasks / Subtasks

- [x] **Task 1: Make push idempotent per batch in `ServerService/Controllers/SyncMeasurementsController.cs`** (AC: #1, #2, #3)
  - [x] 1.1 Inside the `foreach (var batch in batches)` loop, query existing IDs before inserting:
    ```csharp
    var batchIds = batch.Select(dto => dto.Id).ToList();
    var existingIds = (await _db.Measurements
        .Where(m => batchIds.Contains(m.Id))
        .Select(m => m.Id)
        .ToListAsync(cancellationToken))
        .ToHashSet();
    ```
  - [x] 1.2 Filter the batch to only new entities before calling `AddRangeAsync`:
    ```csharp
    var newEntities = batch
        .Where(dto => !existingIds.Contains(dto.Id))
        .Select(dto => new Measurement { ... }).ToList();

    if (newEntities.Count > 0)
    {
        await _db.Measurements.AddRangeAsync(newEntities, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
    ```
  - [x] 1.3 Accumulate inserted count in `insertedCount` (replaces `request.Measurements.Count` in response and SyncRun recording).
  - [x] 1.4 Emit `LogWarning` when duplicates are skipped:
    ```csharp
    if (existingIds.Count > 0)
        _logger.LogWarning(
            "SyncMeasurementsController: [{SyncRunId}] skipped {SkippedCount} already-existing measurement(s) in batch (idempotent re-push).",
            syncRunId, existingIds.Count);
    ```

- [x] **Task 2: Update success response and SyncRun recording to use `insertedCount`** (AC: #2)
  - [x] 2.1 Change `MeasurementCount = request.Measurements.Count` → `MeasurementCount = insertedCount` in the SyncRun entity.
  - [x] 2.2 Change response to:
    ```csharp
    return Ok(new MeasurementPushResponse
    {
        Pushed = insertedCount,
        Message = $"Pushed {insertedCount} new measurements in {batches.Count} batch(es) ({request.Measurements.Count - insertedCount} duplicate(s) skipped)."
    });
    ```

- [x] **Task 3: Null-guard `Request` access** (AC: #4)
  - [x] 3.1 Change:
    ```csharp
    var clientCorrelationId = Request.Headers["X-Correlation-Id"].FirstOrDefault();
    ```
    to:
    ```csharp
    var clientCorrelationId = Request?.Headers["X-Correlation-Id"].FirstOrDefault();
    ```

- [x] **Task 4: Update unit test `Push_DuplicatePush_RollsBackAndReturns500` to reflect new idempotent contract** (AC: #1)
  - [x] 4.1 Rename test to `Push_DuplicatePush_SkipsDuplicatesAndReturns200`.
  - [x] 4.2 Assert the second push returns `OkObjectResult` (HTTP 200) instead of `ObjectResult` with status 500.
  - [x] 4.3 Assert measurement count remains 6 after the second push (no duplicates inserted, no data lost).
  - [x] 4.4 Assert `response.Pushed == 0` when all pushed IDs are duplicates (AC#2 response count verification). *(added in code review)*
  - [x] 4.5 Add `Push_MixedNewAndDuplicate_OnlyNewInserted` test: push 3 existing + 4 new IDs; assert DB count = 7 and `Pushed = 4` (AC#2 mixed-batch scenario). *(added in code review)*

## Files Changed

| File | Change |
|------|--------|
| `ServerService/Controllers/SyncMeasurementsController.cs` | Idempotent batch insert, `insertedCount` tracking, `LogWarning` for skips, `Request?` null-guard |
| `MicroservicesSync.Tests/Measurements/MeasurementPushTests.cs` | Updated duplicate-push test; added mixed new+duplicate test; assert `Pushed` count in response |
| `_bmad-output/planning-artifacts/epics.md` | Added Story 5.3 to Epic 5 summary and detail sections |
