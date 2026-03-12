# Story 3.5: Troubleshooting Flow for Unexpected Sync Outcomes

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a documented troubleshooting flow that uses the summary view, grids, and logs together,
so that I can systematically investigate and resolve unexpected sync outcomes.

## Acceptance Criteria

1. **Given** a sync run appears to produce missing, extra, or duplicated measurements  
   **When** I follow the documented troubleshooting steps in the README or diagnostics section  
   **Then** I am guided to check the sync summary view, relevant jqGrid views, and corresponding logs in a specific order.

2. **Given** I complete the documented troubleshooting steps  
   **When** I re-run the scenario after making any recommended fixes or configuration changes  
   **Then** I can confirm via the same checks that ServerService and all ClientService instances now converge correctly.

3. **Given** a new developer encounters a sync anomaly for the first time  
   **When** they follow the troubleshooting flow  
   **Then** they can independently gather the right evidence (summaries, grid views, logs, and DB queries) to either resolve the issue or escalate it with clear, reproducible details.

## Tasks / Subtasks

- [x] **Task 1: Add "Troubleshooting Unexpected Sync Outcomes" section to README** (AC: #1, #2, #3)
  - [x] 1.1 Open `MicroservicesSync/README.md`. Locate the end of the **"Viewing Sync Logs"** section (the final paragraph ends with the 4-point "Diagnosing a failed sync operation" list). Add the new section immediately after that.
  - [x] 1.2 The new section heading is: `## Troubleshooting Unexpected Sync Outcomes`
  - [x] 1.3 Add an opening paragraph that sets context:
  - [x] 1.4 Add a **"Symptom checklist"** subsection that maps common symptoms to investigation starting points:
  - [x] 1.5 Add the **step-by-step investigation flow** as a numbered section:

    **Investigation steps**

    **Step 1 — Check the Sync Run Summary view**

    Open the ServerService home page at `http://localhost:5000`. The **Sync Runs** grid shows all push and pull operations in reverse-chronological order.

    What to look for:

    - **Status column**: A failed run shows `Failed` status and includes an `ErrorMessage` value. A successful run shows `Success`.
    - **MeasurementCount column**: Verify the count matches what you expected (e.g., `MeasurementsPerClient` × number of clients for a full push cycle).
    - **RunType column**: Confirm the expected `push` and `pull` runs both appear in the correct order.
    - **UserId column**: Verify each client's user GUID appears exactly once per run cycle.

    If a run shows `Failed`, note the `Id` (this is the `SyncRunId`) — you will need it in Step 3.

    If no runs appear at all after triggering sync, the HTTP request may not have reached ServerService. Check container health with `docker-compose ps` before continuing.

    ---

    **Step 2 — Compare measurement counts via jqGrid**

    Open the Measurements grid on ServerService (`http://localhost:5000`) and on each relevant ClientService instance (`http://localhost:5001` through `http://localhost:5005`).

    What to check:

    - **Total count**: After a full push + pull cycle, the total measurement count on every service should be `N clients × MeasurementsPerClient`. The default for the standard scenario is `5 × 10 = 50`.
    - **Per-client counts**: On ServerService, filter by `UserId` for each client and verify each contributes the expected `MeasurementsPerClient` records.
    - **Duplicate detection**: If total count exceeds the expected value, sort by `Id` in the grid. Visible duplicates (same Id appearing twice) indicate a push ran more than once without a clean baseline reset. Run the reset steps from the **Reset to Clean Baseline** section and re-run the scenario.
    - **Missing client data**: If one client's count is `0` after push, check Step 3 (logs) using that client's `UserId` GUID to find the failure.

    The jqGrid supports filtering via the search row — click the magnifying glass icon on the grid header to open filter inputs.

    ---

    **Step 3 — Trace the operation through logs**

    Use docker logs to find the exact server-side and client-side log entries for the failing run.

    **If you have a SyncRunId from Step 1:**

    ```powershell
    # Find all server log entries for that SyncRunId
    docker logs serverservice-app 2>&1 | Select-String "<SyncRunId-here>"
    ```

    The output will show the `BeginScope` structured fields (SyncRunId, RunType, UserId, ClientCorrelationId) and the log message lines. Look for `push failed` or `pull failed` entries with exception details.

    **If you do not have a SyncRunId (push never reached the server):**

    ```powershell
    # Check the client that reported an error
    docker logs clientservice-app-user1 2>&1 | Select-String "failed"

    # Or tail the last 30 lines for a recent failure
    docker logs clientservice-app-user1 --tail 30
    ```

    The ClientService log will contain a `CorrelationId` for the failed push/pull. Use that ID to find the matching server entry:

    ```powershell
    docker logs serverservice-app 2>&1 | Select-String "<CorrelationId-here>"
    ```

    **Common log patterns and what they mean:**

    | Log fragment | Meaning | Action |
    |---|---|---|
    | `push failed for user ... — transaction rolled back` | ServerService rolled back the push transaction | Check the exception messages that follow; likely a constraint violation or DB error |
    | `ServerService rejected push (HTTP 4xx): ...` | ClientService received a non-success HTTP response | Note the status code; 400 means bad request payload; 503 means server not reachable |
    | `pull failed — local transaction rolled back` | ClientService rolled back during a pull | SQLite write error or constraint violation; check for DB file permission issues |
    | `pull returned 0 measurements from ServerService` | Server returned an empty list | Verify ServerService has measurements (Step 1/Step 2); check if push ran before pull |
    | `all N server measurements already present locally` | Pull found all server measurements already in the client DB | Not an error; client convergence is already achieved |
    | `no pending measurements to push` | Client has no unsynced measurements to push | Run measurement generation first (`Generate` button on ClientService home page) |

    ---

    **Step 4 — Inspect the databases directly**

    If Steps 1–3 do not resolve the issue, use direct database inspection to check the raw data.

    **On ServerService (SQL Server):**

    Connect to `localhost,1433` via SSMS (see the [Direct Database Inspection](#direct-database-inspection) section for connection details).

    Run these diagnostic queries:

    ```sql
    -- Check for unexpected duplicates by Id
    SELECT Id, COUNT(*) AS Occurrences
    FROM Measurements
    GROUP BY Id
    HAVING COUNT(*) > 1;
    -- Expected result: 0 rows (no duplicates)

    -- Verify per-user measurement counts
    SELECT UserId, COUNT(*) AS MeasurementCount
    FROM Measurements
    GROUP BY UserId
    ORDER BY MeasurementCount DESC;
    -- Expected after standard scenario: 5 rows, each with MeasurementsPerClient value (default 10)

    -- Identify failed sync runs in the last hour
    SELECT Id, OccurredAt, RunType, UserId, MeasurementCount, Status, ErrorMessage
    FROM SyncRuns
    WHERE Status = 'Failed'
      AND OccurredAt > DATEADD(HOUR, -1, GETUTCDATE())
    ORDER BY OccurredAt DESC;
    ```

    **On each ClientService (SQLite):**

    Copy the SQLite file from the container:

    ```powershell
    docker cp clientservice-app-user1:/app/SqLiteDatabase/ClientServiceDbUser1.sqlite ./ClientServiceDbUser1.sqlite
    ```

    Open in DB Browser for SQLite and run:

    ```sql
    -- Check total measurement count
    SELECT COUNT(*) FROM Measurements;
    -- Should equal the total on ServerService after a successful pull

    -- Check for any measurements with a NULL SyncedAt (not yet pushed)
    SELECT COUNT(*) FROM Measurements WHERE SyncedAt IS NULL;
    -- Expected after a successful push+pull cycle: depends on pulled records
    -- Pulled measurements always have SyncedAt = NULL by design

    -- Check for duplicates
    SELECT Id, COUNT(*) AS Occurrences
    FROM Measurements
    GROUP BY Id
    HAVING COUNT(*) > 1;
    -- Expected result: 0 rows
    ```

    > **Note on SyncedAt semantics:** On ClientService, `SyncedAt` is set when the client successfully *pushed* that measurement to ServerService. Measurements that arrived via *pull* will always have `SyncedAt = NULL`. This is expected behavior. On ServerService, `SyncedAt` is always NULL by design — it is a client-only tracking field.

    ---

    **Step 5 — Confirm convergence after fixing**

    After applying a fix (such as resetting the baseline and re-running the scenario), use this quick convergence checklist:

    1. **Sync Run Summary** (ServerService home page): All runs from the current cycle show `Success` status.
    2. **Measurement grid totals**: ServerService total = `N clients × MeasurementsPerClient`. Each ClientService total matches ServerService's total.
    3. **No duplicates**: The duplicate detection queries in Step 4 return 0 rows on all services.
    4. **Per-client distribution on ServerService**: Each user GUID contributes the expected measurement count.

    If all four checks pass, convergence is confirmed.

  - [x] 1.6 Add a **"When to escalate"** subsection at the end of the troubleshooting section:

    **When to escalate**

    If you cannot resolve the anomaly after Steps 1–5, capture the following information before escalating:

    - The exact scenario steps that reproduce the issue (including reset, generate, push, pull sequence).
    - The SyncRunId(s) from the failed run(s) — found in the Sync Runs grid (`Id` column).
    - The full log output from the relevant containers: `docker logs serverservice-app > server.log` and `docker logs clientservice-app-user<N> > client-userN.log`.
    - The row counts from Step 4 (SQL queries output).
    - The `docker-compose.yml` environment variable section showing current `SyncOptions__MeasurementsPerClient` and `SyncOptions__BatchSize` values.

    This package of information gives any developer enough context to reproduce and diagnose the issue independently.

  - [x] 1.7 Do NOT modify any other README sections. Do NOT change existing "Viewing Sync Logs" or "Direct Database Inspection" sections. The new section is additive only.

## Dev Notes

This is a **documentation-only story**. There are **zero code changes** — no C# files, no configurations, no migrations, no new API endpoints. The entire deliverable is a new README section.

### What This Story Is (and Is Not)

- **IS**: A README section that weaves together the diagnostic capabilities from Stories 3.1–3.4 into a single, step-by-step troubleshooting guide.
- **IS NOT**: A new feature. No new code, no new endpoints, no new UI. All diagnostic tools already exist.

### Where To Insert in the README

The README currently ends with the **"Viewing Sync Logs"** section (added in Story 3.4). The new section goes immediately **after** the Viewing Sync Logs section, at the very end of the file. There is no content after it to worry about ordering.

The section heading to insert after is:
```
**Diagnosing a failed sync operation:**

1. Check the Sync Run Summary view...
...
4. If the failure originated from a ClientService push/pull...
```

The new `## Troubleshooting Unexpected Sync Outcomes` section begins immediately on the next line.

### Diagnostic Tool Map (Full Picture)

All four diagnostic capabilities are already in the codebase:

| Capability | Added in | Location | What it tells you |
|---|---|---|---|
| Sync Run Summary view | Story 3.1 | ServerService home page (`/`) — SyncRuns jqGrid | Which runs happened, their type, user, count, and success/failure status |
| Measurement jqGrid views | Story 3.2 | ServerService and ClientService home pages | Measurement record counts, filtering by user, detecting duplicates |
| Direct DB inspection | Story 3.3 | SSMS → `localhost,1433`; SQLite via `docker cp` | Raw table data, exact counts, duplicate detection via SQL |
| Correlation logging | Story 3.4 | `docker logs <container>` | End-to-end trace of a push/pull via SyncRunId + CorrelationId |

The troubleshooting flow in this story is the glue that shows a developer *how to use these tools together* in a logical investigation sequence.

### SyncedAt Semantics (Critical — Easy to Confuse)

This distinction must be clear in the troubleshooting section because misinterpreting it is a common developer mistake:

- **On ClientService**: `SyncedAt` = set when this client **pushed** this measurement. Always NULL for measurements that arrived via **pull** (even after successful pull). This is expected.
- **On ServerService**: `SyncedAt` is **always NULL** by design. It is a client-only tracking field. A NULL here is not a problem.

If this is not explained, a developer will incorrectly conclude that a pull failed because `SyncedAt` is NULL on pulled measurements.

### Scope Guards — What NOT to Do

- **DO NOT** change any C# files — no controllers, services, domain entities, repositories, or tests.
- **DO NOT** change `docker-compose.yml`, `docker-compose.override.yml`, or any Dockerfiles.
- **DO NOT** add any new endpoints, migrations, or NuGet packages.
- **DO NOT** modify the existing "Direct Database Inspection" section — it was written and completed in Story 3.3.
- **DO NOT** modify the existing "Viewing Sync Logs" section — it was written and completed in Story 3.4.
- **DO NOT** add any UI changes, JavaScript, or Razor view changes.
- **DO NOT** add a separate troubleshooting page, wiki link, or external document — all content goes into the README.

### Architecture Compliance

- **Clean/onion layering:** No changes to any layer.
- **SQL injection safety:** The SQL queries provided in the troubleshooting guide use no dynamic parameters.
- **Scope:** This story is intentionally pure documentation. All referenced tools and infrastructure are operational from Stories 3.1–3.4.

### Project Structure Notes (Documentation Alignment)

- The README lives at: `MicroservicesSync/README.md`
- The troubleshooting section references services by their documented ports: ServerService=`5000`, ClientService instances=`5001`–`5005`
- Container names referenced (`serverservice-app`, `clientservice-app-user1`, ..., `clientservice-app-user5`) must match the names defined in `docker-compose.yml` — verify before finalizing text.
- The `SyncOptions__MeasurementsPerClient` and `SyncOptions__BatchSize` environment variable names must match exactly as they appear in `docker-compose.yml`.

### Previous Story Intelligence (3.4)

From Story 3.4 Dev Notes and completion:
- `BeginScope` with `Dictionary<string, object>` contains: `SyncRunId` (server), `CorrelationId` (client), `RunType`, `UserId`, `ClientCorrelationId`
- Log messages explicitly include the ID in the message template (not just scope), so `Select-String` works even in non-scope-aware log viewers
- `X-Correlation-Id` HTTP header links client's `CorrelationId` to server's `SyncRunId`/`ClientCorrelationId`
- `ILogger.BeginScope` uses `AsyncLocal` state — correlation context is only active during the sync service method execution, not at the controller boundary
- The scope output format is: `=> SyncRunId:abc123, RunType:push, UserId:def456` (rendered by AddSimpleConsole with IncludeScopes=true)

These details directly inform how the `Select-String` commands in the troubleshooting steps should be phrased.

### References

- User story requirements: [planning-artifacts/epics.md — Story 3.5](../../planning-artifacts/epics.md) (Epic 3 section)
- Sync logging details and patterns: [implementation-artifacts/3-4-application-logging-for-sync-operations.md](3-4-application-logging-for-sync-operations.md)
- DB inspection setup: [implementation-artifacts/3-3-direct-database-inspection-support-for-diagnostics.md](3-3-direct-database-inspection-support-for-diagnostics.md)
- SyncRuns table and summary view: [implementation-artifacts/3-1-server-side-sync-run-summary-view.md](3-1-server-side-sync-run-summary-view.md)
- jqGrid measurement views: [implementation-artifacts/3-2-measurement-inspection-via-jqgrid-on-both-services.md](3-2-measurement-inspection-via-jqgrid-on-both-services.md)
- FR12 coverage: Project context at [project-context.md](../../project-context.md) — diagnostics cross-cutting concern
- Architecture document: [planning-artifacts/architecture.md](../../planning-artifacts/architecture.md) — Logging, Diagnostics & Observability section

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot)

### Debug Log References

None — documentation-only story with no code execution required.

### Completion Notes List

- Added `## Troubleshooting Unexpected Sync Outcomes` section to `MicroservicesSync/README.md` immediately after the existing "Viewing Sync Logs" section's 4-point diagnostic list.
- Section contains: opening paragraph, symptom checklist table (6 rows mapping symptoms to starting steps), and full 5-step investigation flow covering Sync Run Summary view, jqGrid comparison, log tracing, direct DB inspection, and convergence confirmation.
- Includes log pattern reference table, SQL diagnostic queries for both SQL Server and SQLite, `docker cp` extraction command, and `SyncedAt` semantics note.
- "When to escalate" subsection provides a complete evidence package checklist for escalation.
- No existing README sections were modified; the new section is purely additive.
- Container names verified against `docker-compose.yml`: `serverservice-app`, `clientservice-app-user1` through `clientservice-app-user5`.
- All 3 Acceptance Criteria satisfied: AC1 (guided troubleshooting order via Summary→Grid→Logs→DB), AC2 (convergence checklist in Step 5), AC3 (self-contained guide for new developers with escalation path).

### File List

- `MicroservicesSync/README.md` (modified — added Troubleshooting Unexpected Sync Outcomes section; code review: fixed misleading SQL comment and unsearchable log pattern fragment)

## Change Log

- 2026-03-12: Code review fixes — corrected duplicate task markers (1.6/1.7 appeared as both [x] and [ ]), fixed misleading SQL comment for `SyncedAt IS NULL` query, fixed unsearchable log pattern fragment `all N` → `all <N>` in README log table
- 2026-03-12: Added "Troubleshooting Unexpected Sync Outcomes" section to README.md — documentation-only, no code changes (Story 3.5)
