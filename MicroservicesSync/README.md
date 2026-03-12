# Microserices-Sync

Local development environment for multi-client sync experiments.

## Prerequisites

| Tool | Version / Note |
|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest stable — required to run all services |
| [Git](https://git-scm.com/) | Any recent version — required to clone the repository |
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Required only if running or building outside Docker (optional for Docker-only workflow) |
| [Visual Studio 2022 (17.12+)](https://visualstudio.microsoft.com/vs/) or [VS Code](https://code.visualstudio.com/) | Required for local debugging outside containers; Visual Studio is the primary dev environment |
| [SQL Server Management Studio (SSMS)](https://aka.ms/ssms) | Optional — required for Direct Database Inspection of SQL Server |
| [DB Browser for SQLite](https://sqlitebrowser.org) | Optional — required for Direct Database Inspection of SQLite |

> SSMS and DB Browser are needed only if you want to inspect databases directly (see [Direct Database Inspection](#direct-database-inspection)). They are not required to run the standard Docker scenario.

You will also need a `.env` file in this folder (see Quick Start below) — it is git-ignored and must be created manually on each machine.

## Quick Start

### Before your first run

1. **Clone the repository** (skip if you already have it locally):
   ```bash
   git clone <repository-url>
   cd <repository-folder>
   cd MicroservicesSync
   ```

2. **Create the required `.env` file** in the `MicroservicesSync/` folder (the same folder as `docker-compose.yml`):
   ```
   SA_PASSWORD=<YourStrong@Passw0rd>
   ```
   Replace `<YourStrong@Passw0rd>` with your chosen SQL Server password.  
   The password must meet SQL Server's complexity requirements:
   - At least 8 characters
   - Contains uppercase letters, lowercase letters, digits, and at least one symbol

   This file is git-ignored and must be created manually on each machine.  
   Without it, the `sqlserver` container will fail to start because `SA_PASSWORD` is undefined.

### Start the services

> Ensure Docker Desktop is running before executing these commands.

```bash
# First run / after code changes (first run downloads images and may take several minutes)
docker-compose up --build

# Subsequent runs (images already built)
docker-compose up

# Stop all containers
docker-compose down
```

## Verifying the Environment is Healthy

Once all containers are running, check each service's `/health` endpoint.
Every endpoint should return HTTP 200 with `{"status":"Healthy"}`.

| Service               | Health URL                          |
|-----------------------|-------------------------------------|
| ServerService         | http://localhost:5000/health        |
| ClientService user 1  | http://localhost:5001/health        |
| ClientService user 2  | http://localhost:5002/health        |
| ClientService user 3  | http://localhost:5003/health        |
| ClientService user 4  | http://localhost:5004/health        |
| ClientService user 5  | http://localhost:5005/health        |

You can also check container health status at a glance:

```bash
docker-compose ps
# All application containers should show (healthy) next to their running state.
```

A ClientService instance will report `{"status":"Unhealthy"}` (HTTP 503) if its
`ClientIdentity__UserId` environment variable is missing or not a valid GUID.

### Accessing the Service Home Pages

Once all health checks return `{"status":"Healthy"}`, open the service home pages in a browser to confirm the UI and data layer are fully operational (migrations applied, reference data seeded, UI rendering):

| Service | Home Page URL | What you should see |
|---|---|---|
| ServerService | http://localhost:5000 | jqGrid tables for Measurements, Buildings, Rooms, Surfaces, Cells, Users, and Sync Runs. On first run: 0 measurements, 5 users, 2 buildings, 4 rooms, 8 surfaces, 16 cells. |
| ClientService User 1 | http://localhost:5001 | jqGrid tables for Measurements (CRUD) and read-only reference grids. On first run: all tables empty until **Pull Reference Data** is triggered. Sync action buttons: **Generate**, **Push**, **Pull**, **Reset Client DB**, **Pull Reference Data**. |
| ClientService User 2–5 | http://localhost:5002 through http://localhost:5005 | Same as User 1 (each backed by its own isolated SQLite database). |

> Health endpoint checks (`/health`) confirm container liveness. The home page confirms end-to-end readiness: DB migrations applied, reference data seeded, and UI rendering correctly.

> If ServerService's home page loads but shows 0 rows in all grids with no error, seeding may still be in progress — wait 5–10 seconds and refresh. If tables remain empty after 30 seconds, check the `serverservice-app` logs with `docker logs serverservice-app --tail 30` for seeding errors.

## Scenario Parameters

Control experiment inputs via environment variables in `docker-compose.yml` — no recompilation needed.

| Environment Variable               | Config path (ASP.NET notation)              | Default | Description                                                       |
|------------------------------------|---------------------------------------------|---------|-------------------------------------------------------------------|
| `SyncOptions__MeasurementsPerClient` | `SyncOptions:MeasurementsPerClient`        | `10`    | Number of measurements generated per ClientService per scenario run |
| `SyncOptions__BatchSize`           | `SyncOptions:BatchSize`                     | `5`     | Records per in-memory batch during a push/pull sync operation     |

To override for a single run, edit the matching `environment:` entry under the relevant service in `docker-compose.yml`, then run `docker-compose up`.

### Changing Client Count

The default topology uses 5 ClientService instances (`clientservice_user1` through `clientservice_user5`), as defined in ADR-002.

To add a 6th client:
1. Copy any `clientservice_userN` block in `docker-compose.yml`, rename it `clientservice_user6`, and update the port mapping (`"5006:8080"`), SQLite path, `ClientIdentity__UserId` (new stable GUID), and volume names.
2. Add the matching volume declarations at the bottom of `docker-compose.yml`.
3. Ensure the GUID matches a seeded user entry (Story 1.4 seed data).

To remove a client: delete its service block and its volumes declaration.

## Reset to Clean Baseline

Use these steps to delete all measurements and restore each service to its fresh-install state
(ServerService re-seeded with reference data; each ClientService empty and ready to pull reference data).

**Prerequisites:** all containers running (`docker-compose up`).

**Step 1 — Reset ServerService** (deletes all measurements, re-seeds reference data):

```bash
curl -X POST http://localhost:5000/api/v1/admin/reset
```

Expected response: `{"message":"ServerService reset complete."}`

**Step 2 — Reset each ClientService** (repeat for ports 5001–5005):

```bash
curl -X POST http://localhost:5001/api/v1/admin/reset
curl -X POST http://localhost:5002/api/v1/admin/reset
curl -X POST http://localhost:5003/api/v1/admin/reset
curl -X POST http://localhost:5004/api/v1/admin/reset
curl -X POST http://localhost:5005/api/v1/admin/reset
```

Expected response: `{"message":"ClientService reset complete. Restart or trigger reference pull to reload reference data."}`

**Step 3 — Reload reference data on each ClientService** (or restart the containers):

```bash
curl -X POST http://localhost:5001/api/v1/admin/pull-reference-data
curl -X POST http://localhost:5002/api/v1/admin/pull-reference-data
curl -X POST http://localhost:5003/api/v1/admin/pull-reference-data
curl -X POST http://localhost:5004/api/v1/admin/pull-reference-data
curl -X POST http://localhost:5005/api/v1/admin/pull-reference-data
```

Expected response: `{"message":"Reference data loaded."}`

**Alternatively** — use the UI buttons on each service's home page:
- ServerService (`http://localhost:5000`): click **Reset to Baseline**
- Each ClientService (`http://localhost:5001` – `5005`): click **Reset Client DB**, then **Pull Reference Data**

**Expected clean state:**

| Entity        | ServerService | Each ClientService |
|---------------|---------------|--------------------|
| Users         | 5             | 5                  |
| Buildings     | 2             | 2                  |
| Rooms         | 4             | 4                  |
| Surfaces      | 8             | 8                  |
| Cells         | 16            | 16                 |
| Measurements  | 0             | 0                  |

> After a reset, the expected table counts listed in the [Direct Database Inspection](#direct-database-inspection) section serve as the verification baseline for direct DB queries.

## Running Tests

```bash
dotnet test MicroservicesSync.Tests/MicroservicesSync.Tests.csproj
```

> To verify data correctness beyond the tests, see the [Direct Database Inspection](#direct-database-inspection) section below.

## Direct Database Inspection

Use the tools in this section to verify data correctness beyond the UI — useful when diagnosing sync issues or confirming that a scenario completed as expected.

### SQL Server (ServerService) — SSMS

SQL Server port `1433` is already exposed in `docker-compose.yml` (`ports: - "1433:1433"` under the `sqlserver` service) — no docker-compose changes are needed.

**SSMS connection parameters:**

| Parameter                | Value                                    |
|--------------------------|------------------------------------------|
| Server                   | `localhost,1433`                         |
| Authentication           | SQL Server Authentication                |
| Login                    | `sa`                                     |
| Password                 | Value from `.env` file (`SA_PASSWORD`)   |
| Trust Server Certificate | Checked (required — container uses a self-signed cert) |
| Database to explore      | `ServerServiceDb`                        |

**Key tables:**

| Table          | Description                                              |
|----------------|----------------------------------------------------------|
| `Measurements` | All measurements pushed by all clients                   |
| `SyncRuns`     | Summary records for each push/pull sync run (added in Story 3.1) |
| `Users`        | 5 seeded users; each has a stable `UserId` GUID          |
| `Buildings`    | 2 seeded buildings                                       |
| `Rooms`        | 4 seeded rooms                                           |
| `Surfaces`     | 8 seeded surfaces                                        |
| `Cells`        | 16 seeded cells                                          |

**Example diagnostic queries:**

```sql
-- Count measurements per user/client
SELECT UserId, COUNT(*) AS MeasurementCount
FROM Measurements
GROUP BY UserId
ORDER BY MeasurementCount DESC;

-- View all sync runs (most recent first)
SELECT OccurredAt, RunType, UserId, MeasurementCount, Status, ErrorMessage
FROM SyncRuns
ORDER BY OccurredAt DESC;

-- Verify convergence: compare ServerService measurement count with expected total
SELECT COUNT(*) AS TotalMeasurements FROM Measurements;
-- Expected after 5-client standard run: 5 clients × MeasurementsPerClient value
```

> **Note on `SyncedAt`:** In the `Measurements` table on the server, `SyncedAt` is always NULL by design — it is a client-side tracking field only. This is expected behaviour, not a data problem.

> **Note on `RowVersion`:** Most tables (`Measurements`, `Users`, `Buildings`, `Rooms`, `Surfaces`, `Cells`) include a `RowVersion` (`timestamp`) column used as a concurrency token. It is a binary value managed automatically by SQL Server — safe to ignore during inspection. `SyncRuns` does not have a `RowVersion` column.

### SQLite (ClientService) — DB Browser for SQLite

Recommended viewer: **DB Browser for SQLite** ([https://sqlitebrowser.org](https://sqlitebrowser.org)) — free, cross-platform, no login required.

Each ClientService container stores its SQLite database inside the container at `/app/SqLiteDatabase/<filename>`. Use `docker cp` to copy a snapshot to your host:

**Method A — Docker cp (recommended):**

```powershell
# For User 1:
docker cp clientservice-app-user1:/app/SqLiteDatabase/ClientServiceDbUser1.sqlite ./ClientServiceDbUser1.sqlite
# For User 2–5: substitute the container name and filename accordingly
# Container names: clientservice-app-user2, ..., clientservice-app-user5
# File names:       ClientServiceDbUser2.sqlite, ..., ClientServiceDbUser5.sqlite
```

**Method B — Bind-mount path (not applicable by default):**

> The default `docker-compose.yml` uses named volumes (not host bind mounts), so there is no host directory path to open directly. Method A (`docker cp`) is the canonical approach. The file extracted is a point-in-time snapshot — run `docker cp` again after additional sync activity to capture updated data.

**Key tables:**

| Table          | Description                                               |
|----------------|-----------------------------------------------------------|
| `Measurements` | This client's local measurements (may include synced)     |
| `Users`        | 5 users pulled from ServerService during reference sync   |
| `Buildings`    | 2 buildings pulled from ServerService                     |
| `Rooms`        | 4 rooms pulled from ServerService                         |
| `Surfaces`     | 8 surfaces pulled from ServerService                      |
| `Cells`        | 16 cells pulled from ServerService                        |

**Example diagnostic queries (SQLite):**

```sql
-- Count local measurements on this client
SELECT COUNT(*) FROM Measurements;

-- Inspect measurement details
SELECT Id, UserId, RecordedAt, SyncedAt, Value
FROM Measurements
ORDER BY RecordedAt DESC
LIMIT 20;

-- Verify reference data was pulled correctly
SELECT COUNT(*) FROM Users;      -- expected: 5
SELECT COUNT(*) FROM Buildings;  -- expected: 2
SELECT COUNT(*) FROM Rooms;      -- expected: 4
SELECT COUNT(*) FROM Surfaces;   -- expected: 8
SELECT COUNT(*) FROM Cells;      -- expected: 16
```

> **Note on `SyncedAt`:** In the ClientService `Measurements` table, `SyncedAt` is set to a UTC timestamp when a measurement was successfully **pushed** to ServerService by this client. A NULL value means either (a) the measurement was generated locally but not yet pushed, or (b) the measurement was **pulled** from another client — pulled measurements always arrive with `SyncedAt = null`.

> **Note on `ConcurrencyStamp`:** Tables include a `ConcurrencyStamp` (integer) column used as a concurrency token. Safe to ignore during inspection.

> For log-based diagnostics, see the **Viewing Sync Logs** section below.

## Viewing Sync Logs

Sync operations on both ServerService and ClientService emit structured log entries that include a **correlation ID**, user identity, operation type, measurement count, and success/failure status.

**Viewing logs via docker logs:**

```powershell
# ServerService logs (most recent 50 lines)
docker logs serverservice-app --tail 50

# ClientService User 1 logs
docker logs clientservice-app-user1 --tail 50

# Filter logs by timestamp range
docker logs serverservice-app --since "2026-03-11T10:00:00" --until "2026-03-11T11:00:00"
```

**Filtering by correlation ID or SyncRunId:**

Each sync operation generates a unique ID. On ServerService, it is the `SyncRunId` (matches the `Id` column in the `SyncRuns` table). On ClientService, it is a `CorrelationId` passed to the server as an `X-Correlation-Id` HTTP header.

```powershell
# Find all log entries for a specific ServerService SyncRunId
docker logs serverservice-app 2>&1 | Select-String "SyncRunId: <paste-id-here>"

# Find all log entries for a specific ClientService CorrelationId
docker logs clientservice-app-user1 2>&1 | Select-String "CorrelationId: <paste-id-here>"

# Trace a client operation end-to-end (client + server logs)
docker logs clientservice-app-user1 2>&1 | Select-String "<correlation-id>"
docker logs serverservice-app 2>&1 | Select-String "<correlation-id>"
```

**Log entry fields for sync operations:**

| Field | Description | Service |
|---|---|---|
| SyncRunId | Unique ID for a sync run on ServerService (matches `SyncRuns.Id` in SQL Server) | ServerService |
| CorrelationId | Unique ID for a sync operation on ClientService (passed as `X-Correlation-Id` header) | ClientService |
| RunType | `push` or `pull` | Both |
| UserId | GUID of the user/client performing the operation | Both |
| ClientCorrelationId | The client's CorrelationId as received by ServerService (via HTTP header) | ServerService |

**Diagnosing a failed sync operation:**

1. Check the Sync Run Summary view on ServerService home page — failed runs show error status.
2. Note the `SyncRunId` from the summary view (it is the `Id` column).
3. Use `docker logs serverservice-app 2>&1 | Select-String "<SyncRunId>"` to find server-side details.
4. If the failure originated from a ClientService push/pull, check the client's logs for the corresponding `CorrelationId` and the `X-Correlation-Id` in the server logs.

## Troubleshooting Unexpected Sync Outcomes

Use this section when a scenario run produces results that do not match expectations — for example, measurement counts that differ between ServerService and a ClientService instance, duplicated records, or a failed sync run. The steps below guide you through a systematic investigation using all the diagnostic tools available in the environment.

**Symptom checklist**

| Symptom | Start here |
|---|---|
| Push or pull button returned an error | Step 1 (Sync Run Summary) |
| Measurement count on a client does not match the server after pull | Step 1, then Step 2 |
| ServerService count is lower than expected after push | Step 1, then Step 3 |
| Duplicate measurements appear in a jqGrid | Step 2, then Step 4 |
| A scenario that used to converge has started failing | Step 3 (logs), then Step 4 (DB) |
| No measurements appear at all after a full push/pull cycle | Step 1 first; verify clean baseline precondition |

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
| `all <N> server measurements already present locally` | Pull found all server measurements already in the client DB | Not an error; client convergence is already achieved |
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

-- Count NULL SyncedAt: includes unsent measurements AND all pulled records (see SyncedAt semantics note below)
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

**When to escalate**

If you cannot resolve the anomaly after Steps 1–5, capture the following information before escalating:

- The exact scenario steps that reproduce the issue (including reset, generate, push, pull sequence).
- The SyncRunId(s) from the failed run(s) — found in the Sync Runs grid (`Id` column).
- The full log output from the relevant containers: `docker logs serverservice-app > server.log` and `docker logs clientservice-app-user<N> > client-userN.log`.
- The row counts from Step 4 (SQL queries output).
- The `docker-compose.yml` environment variable section showing current `SyncOptions__MeasurementsPerClient` and `SyncOptions__BatchSize` values.

This package of information gives any developer enough context to reproduce and diagnose the issue independently.
