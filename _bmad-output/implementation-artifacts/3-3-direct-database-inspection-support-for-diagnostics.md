# Story 3.3: Direct Database Inspection Support for Diagnostics

Status: ready-for-dev

## Story

As a developer,
I want the environment to support direct database inspection on both SQL Server and SQLite,
So that I can verify data correctness beyond the UI when diagnosing sync issues.

## Acceptance Criteria

1. **Given** the environment is running  
   **When** I use SQL Server Management Studio to connect to the ServerService database using the documented connection details  
   **Then** I can query the Measurements and reference tables and see data that matches what is displayed in the ServerService UI.

2. **Given** I use a SQLite viewer against a ClientService database file using the documented path  
   **When** I query the Measurements table  
   **Then** I see rows and counts that match the ClientService Measurements jqGrid and expected sync results.

3. **Given** a new developer follows the README instructions for DB inspection  
   **When** they connect to the databases for the first time  
   **Then** they can successfully locate the relevant tables and confirm that data matches the documented scenarios.

## Tasks / Subtasks

- [ ] **Task 1: Document SQL Server SSMS connection details in README** (AC: #1, #3)
  - [ ] 1.1 Add a new "Direct Database Inspection" section to `MicroservicesSync/README.md` (after the "Running Tests" section).
  - [ ] 1.2 In the SQL Server subsection, document the SSMS connection parameters:
    - **Server:** `localhost,1433`
    - **Authentication:** SQL Server Authentication
    - **Login:** `sa`
    - **Password:** value from the `.env` file (`SA_PASSWORD`)
    - **Trust Server Certificate:** checked (required because the container uses a self-signed cert)
    - **Database to explore:** `ServerServiceDb`
  - [ ] 1.3 List the key tables a developer should inspect:

    | Table            | Description                                                 |
    |------------------|-------------------------------------------------------------|
    | `Measurements`   | All measurements pushed by all clients                      |
    | `SyncRuns`       | Summary records for each push/pull sync run (added in 3.1) |
    | `Users`          | 5 seeded users; each has a stable `UserId` GUID             |
    | `Buildings`      | 2 seeded buildings                                          |
    | `Rooms`          | 4 seeded rooms                                              |
    | `Surfaces`       | 8 seeded surfaces                                           |
    | `Cells`          | 16 seeded cells                                             |

  - [ ] 1.4 Provide example SSMS queries for common diagnostics:
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
  - [ ] 1.5 Add a note that SQL Server port `1433` is already exposed in `docker-compose.yml` (line: `ports: - "1433:1433"` under the `sqlserver` service) — no docker-compose changes are needed to enable SSMS connectivity.

- [ ] **Task 2: Document SQLite database file paths for ClientService inspection** (AC: #2, #3)
  - [ ] 2.1 In the new "Direct Database Inspection" section, add a SQLite subsection.
  - [ ] 2.2 Document the recommended SQLite viewer: **DB Browser for SQLite** ([https://sqlitebrowser.org](https://sqlitebrowser.org)) — free, cross-platform, no login required.
  - [ ] 2.3 Document two methods to access each ClientService SQLite file:

    **Method A — Docker volume inspection (recommended for in-place diagnostics):**

    Each ClientService SQLite database lives inside a Docker volume. To copy it to the host for a viewer:
    ```powershell
    # For User 1:
    docker cp clientservice-app-user1:/app/SqLiteDatabase/ClientServiceDbUser1.sqlite ./ClientServiceDbUser1.sqlite
    # For User 2–5: substitute the container name and filename accordingly
    # Container names: clientservice-app-user2, ..., clientservice-app-user5
    # File names:       ClientServiceDbUser2.sqlite, ..., ClientServiceDbUser5.sqlite
    ```

    **Method B — Bind-mount path (if volumes are mapped to host directories):**
    > The default `docker-compose.yml` uses named volumes (not host bind mounts), so Method A is the canonical approach.

  - [ ] 2.4 List the key tables in a ClientService SQLite database:

    | Table           | Description                                              |
    |-----------------|----------------------------------------------------------|
    | `Measurements`  | This client's local measurements (may include synced)    |
    | `Users`         | 5 users pulled from ServerService during reference sync  |
    | `Buildings`     | 2 buildings pulled from ServerService                    |
    | `Rooms`         | 4 rooms pulled from ServerService                        |
    | `Surfaces`      | 8 surfaces pulled from ServerService                     |
    | `Cells`         | 16 cells pulled from ServerService                       |

  - [ ] 2.5 Provide example SQL queries for a SQLite viewer:
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
  - [ ] 2.6 Add a note that the SQLite file extracted via `docker cp` is a point-in-time snapshot. Running `docker cp` again after additional sync activity will capture updated data.

- [ ] **Task 3: Cross-reference the new DB Inspection section from relevant README sections** (AC: #3)
  - [ ] 3.1 In the existing "Running Tests" section of the README, add a reference sentence pointing developers to the "Direct Database Inspection" section when they want to verify data outside of tests.
  - [ ] 3.2 In the existing "Reset to Clean Baseline" section, add a note that after reset the expected table counts listed in the "Direct Database Inspection" section serve as the verification baseline.

- [ ] **Task 4: Smoke test — verify SSMS and SQLite viewer connectivity** (AC: #1, #2)
  - [ ] 4.1 Start the environment: `docker-compose up` (from `MicroservicesSync/`).
  - [ ] 4.2 Open SSMS and connect using the documented parameters (Task 1.2). Confirm:
    - Connection succeeds without certificate errors.
    - `ServerServiceDb` is visible in Object Explorer.
    - All tables listed in Task 1.3 are present.
    - Running the measurement count query returns a result (0 or more rows, no SQL errors).
  - [ ] 4.3 Copy a ClientService SQLite file using the `docker cp` command from Task 2.3 (Method A). Open it in DB Browser for SQLite. Confirm:
    - All tables listed in Task 2.4 are present.
    - Running the reference-data count queries (Task 2.5) returns the expected counts after a reference pull.
  - [ ] 4.4 Run `docker-compose down` to confirm clean shutdown (exit 0).

## Dev Notes

This is a **documentation-only story** — no production code, migrations, or new application files are required.

The entire scope is:
1. A new "Direct Database Inspection" section in `README.md`.
2. Two cross-reference additions in existing README sections.

### What NOT to Do (Scope Guards)

- **DO NOT** modify any `.cs` files, Dockerfiles, or `docker-compose.yml`.
- **DO NOT** change any EF Core migrations, DbContexts, or entity classes.
- **DO NOT** add or modify any API controllers, Application services, or Infrastructure repositories.
- **DO NOT** create any test files as part of this story.
- **DO NOT** add bind-mount volume overrides to docker-compose for SQLite access — the `docker cp` approach is sufficient and keeps the compose file clean.
- **DO NOT** change port mappings — SQL Server port 1433 is already exposed in `docker-compose.yml`.

### README Location and Structure

The README file is at `MicroservicesSync/README.md`. The new section should be appended after the existing **"Running Tests"** section so the flow reads:

1. Prerequisites
2. Quick Start
3. Verifying the Environment is Healthy
4. Scenario Parameters → Changing Client Count
5. Reset to Clean Baseline
6. Running Tests
7. **Direct Database Inspection** ← NEW SECTION HERE

### SQL Server Connectivity Context

- SQL Server port `1433` is already bound to `localhost:1433` in `docker-compose.yml` under the `sqlserver` service (`ports: - "1433:1433"`). No docker-compose change is needed.
- The SA password is stored in a `.env` file at the `MicroservicesSync/` root (required pre-existing setup; documented in README prerequisites).
- The database name is `ServerServiceDb` (see `ConnectionStrings__DefaultConnection` in `docker-compose.yml` under the `serverservice` service).
- **TrustServerCertificate=True** is required because the containerized SQL Server uses a self-signed certificate; SSMS must have "Trust Server Certificate" checked or the connection will fail with a certificate error.
- The `SyncRuns` table (added in Story 3.1) will be present in the database if at least one sync run has been performed after applying the migration from 3.1.

### SQLite File Paths in Docker

Each ClientService container stores its SQLite database at `/app/SqLiteDatabase/<filename>` inside the container. The filenames match the `ConnectionStrings__DefaultConnection` values in `docker-compose.yml`:

| Container                   | File inside container                         |
|-----------------------------|-----------------------------------------------|
| `clientservice-app-user1`   | `/app/SqLiteDatabase/ClientServiceDbUser1.sqlite` |
| `clientservice-app-user2`   | `/app/SqLiteDatabase/ClientServiceDbUser2.sqlite` |
| `clientservice-app-user3`   | `/app/SqLiteDatabase/ClientServiceDbUser3.sqlite` |
| `clientservice-app-user4`   | `/app/SqLiteDatabase/ClientServiceDbUser4.sqlite` |
| `clientservice-app-user5`   | `/app/SqLiteDatabase/ClientServiceDbUser5.sqlite` |

Container names (used with `docker cp`): `clientservice-app-user1` through `clientservice-app-user5`.

### Concurrency Token Columns in DB Viewers

When inspecting tables in SSMS or DB Browser, note:
- **SQL Server:** `Measurements`, `Users`, `Buildings`, `Rooms`, `Surfaces`, `Cells` all have a `RowVersion` (`timestamp`) column — this is the concurrency token and is managed automatically by SQL Server. It is a binary value; you can safely ignore it during diagnostic inspections.
- **SQLite:** Equivalent tables have a `ConcurrencyStamp` (integer/long) column for the same purpose. Again, ignore it during inspection — it does not represent human-readable data.

### `SyncedAt` Column Semantics (Important for Diagnostics)

When inspecting the `Measurements` table:
- **On ClientService SQLite:** `SyncedAt` is set to a UTC timestamp when the measurement was successfully pushed to ServerService. If `SyncedAt` is NULL, the measurement was generated locally but not yet pushed.
- **On ServerService SQL Server:** `SyncedAt` is always NULL — this is by design. ServerService does not set `SyncedAt`; it is a client-side tracking field only.

This asymmetry was established in Epic 2 and is expected behavior, not a data problem.

### Previous Story Learnings

**From Story 3.1 (Server-Side Sync Run Summary View):**
- The `SyncRun` entity and `SyncRuns` table were added to `Sync.Domain` and `ServerDbContext` respectively. SSMS documentation in this story must reference this table.
- Story 3.1's migration name is `AddSyncRunTable`; it creates the `SyncRuns` table in `ServerServiceDb`.

**From Story 3.2 (Measurement Inspection via jqGrid on Both Services):**
- `Sync.Infrastructure/Grid/JqGridFilter.cs` and `JqGridHelper.cs` were added — documentation-only story 3.3 does not touch these files.
- Story 3.2 confirmed `docker-compose down` exits cleanly (exit 0). Environment is stable.

**From Epic 2 Retrospective (2026-03-09):**
- PREP-1 (ReferenceDataLoader extraction) status was unclear as of the retro. This story does not depend on it, but the Dev agent should NOT touch `Program.cs` or `ReferenceDataLoader`.
- The `// NOTE (M2.x)` direct-DbContext injection pattern is acknowledged tech debt. Do not add new deviations.

### Project Structure Notes

This story only modifies `MicroservicesSync/README.md`. No other files in the solution should be touched.

**Source paths consulted for this story:**
- `MicroservicesSync/docker-compose.yml` — SQL Server port mappings and ClientService container names/SQLite paths
- `_bmad-output/planning-artifacts/architecture.md` — NFR12 (logs/diagnostics), deployment/observability section
- `_bmad-output/planning-artifacts/epics.md` — Story 3.3 acceptance criteria (FR11 partial, FR12)
- `_bmad-output/implementation-artifacts/3-1-server-side-sync-run-summary-view.md` — SyncRun entity/table details
- `_bmad-output/implementation-artifacts/epic-2-retro-2026-03-09.md` — learnings relevant to Epic 3

### References

- [Story 3.3 epics spec](../_bmad-output/planning-artifacts/epics.md#story-33-direct-database-inspection-support-for-diagnostics)
- [Architecture — Monitoring and observability](../_bmad-output/planning-artifacts/architecture.md#monitoring-and-observability)
- [Architecture — Persistence and volumes](../_bmad-output/planning-artifacts/architecture.md#persistence-and-volumes)
- [docker-compose.yml — sqlserver service ports](../MicroservicesSync/docker-compose.yml)
- [Story 3.1 — SyncRun entity](../3-1-server-side-sync-run-summary-view.md)
- [Project Context — Critical Don't-Miss Rules](../_bmad-output/project-context.md#critical-dont-miss-rules)

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot)

### Debug Log References

### Completion Notes List

### File List

- `MicroservicesSync/README.md` — Add "Direct Database Inspection" section and two cross-references
