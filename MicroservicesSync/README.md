# Microserices-Sync

Local development environment for multi-client sync experiments.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- A `.env` file in this folder containing `SA_PASSWORD=<YourStrong@Passw0rd>`

## Quick Start

```bash
# First run / after code changes
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
