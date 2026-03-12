# Story 4.1: Single-Entry README with Prerequisites and Quickstart

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a single README that lists prerequisites and quickstart steps,
so that I can bring up the environment and run the core scenario without hunting through multiple documents.

## Acceptance Criteria

1. **Given** I open the repository root  
   **When** I open the main README file  
   **Then** I see a clear prerequisites section that lists required tools and versions (Docker, SQL Server/containers, SQLite viewer, and Visual Studio) aligned with the PRD and architecture.

2. **Given** I have the prerequisites installed  
   **When** I follow the quickstart section step by step  
   **Then** I can clone the repo, configure any required environment variables, and start all services using the documented single command or short sequence.

3. **Given** the environment has started successfully  
   **When** I follow the remaining quickstart steps  
   **Then** I can reach the ServerService and ClientService home pages and confirm basic health using the documented checks.

## Tasks / Subtasks

- [ ] **Task 1: Audit the current README structure and identify gaps** (AC: #1, #2, #3)
  - [ ] 1.1 Open `MicroservicesSync/README.md`. Read the full file from top to bottom.
  - [ ] 1.2 Note: the README already has substantial content from Epics 1–3. This story is **audit, complete, and integrate** — NOT rewrite from scratch. Preserve all existing sections exactly as written: Reset to Clean Baseline, Running Tests, Direct Database Inspection, Viewing Sync Logs, Troubleshooting Unexpected Sync Outcomes.
  - [ ] 1.3 Identify the three concrete gaps against the ACs (detailed in Dev Notes).

- [ ] **Task 2: Enhance the Prerequisites section** (AC: #1)
  - [ ] 2.1 Locate the `## Prerequisites` section at the top of `README.md`. It currently lists only Docker Desktop and a `.env` file.
  - [ ] 2.2 Replace the Prerequisites section with an expanded version that lists all required tools with versions. See Dev Notes for the exact required list.
  - [ ] 2.3 Ensure the `.env` file requirement is retained and its purpose is clearly stated.

- [ ] **Task 3: Enhance the Quick Start section to include clone and .env setup steps** (AC: #2)
  - [ ] 3.1 Locate the `## Quick Start` section. It currently starts directly with `docker-compose up --build` — there is no `git clone` step.
  - [ ] 3.2 Add a `git clone` step as the first step in the Quick Start sequence (before docker-compose commands). Use a placeholder repo URL since the actual remote URL may vary per developer.
  - [ ] 3.3 Add an explicit `.env` file creation step immediately after clone (before docker-compose). The step must explain what the file must contain (`SA_PASSWORD=<YourStrong@Passw0rd>`), what the password constraints are (SQL Server strong password policy), and where the file must be placed (`MicroservicesSync/` folder, alongside `docker-compose.yml`).
  - [ ] 3.4 Retain the existing docker-compose commands (`docker-compose up --build`, `docker-compose up`, `docker-compose down`) unchanged — they are correct and well-established.

- [ ] **Task 4: Add home page navigation step to the health verification section** (AC: #3)
  - [ ] 4.1 Locate the `## Verifying the Environment is Healthy` section. It documents the `/health` endpoint table and `docker-compose ps` — but it does NOT mention navigating to the service home pages.
  - [ ] 4.2 Add a paragraph or subsection **after** the health endpoint table that directs the developer to open the service home pages in a browser:
    - ServerService: `http://localhost:5000` — expect the main jqGrid interface with Measurements, Buildings, Rooms, Surfaces, Cells, Users, and Sync Runs grids.
    - Each ClientService instance: `http://localhost:5001` through `http://localhost:5005` — expect the main jqGrid interface with read-only reference data grids and Measurements CRUD + sync actions (Generate, Push, Pull, Reset Client DB, Pull Reference Data buttons).
  - [ ] 4.3 Frame this step as completing the "environment started successfully" confirmation — health endpoints confirm the process is alive; the home page confirms the UI and data layer is working.

- [ ] **Task 5: Final consistency review** (AC: #1, #2, #3)
  - [ ] 5.1 Read the updated README from top to bottom and verify the flow makes logical sense for a developer picking up the project for the first time: Prerequisites → Clone → Create .env → Start services → Health check → Open home pages → then the rest of the existing sections.
  - [ ] 5.2 Verify no duplicate tool mentions between the new Prerequisites section and the existing "Direct Database Inspection" section (which separately documents SSMS and DB Browser for SQLite with detailed connection steps). The Prerequisites section should list the tool and version; the Deep-dive sections provide usage details — no duplication needed.
  - [ ] 5.3 Do NOT reorganize or restructure the body of the README beyond the three targeted changes above. Do NOT modify: any `.cs` files, `Dockerfiles`, `docker-compose.yml`, `docker-compose.override.yml`, test projects, or migration files. This is a documentation-only story.

## Dev Notes

### Context: README State Entering Story 4.1

The `MicroservicesSync/README.md` file already contains substantial content added incrementally across Epics 1–3:

| Section | Added in | Status |
|---|---|---|
| Prerequisites | Story 1.1 | Incomplete — only Docker + .env listed |
| Quick Start | Story 1.1 | Incomplete — no clone or .env creation steps |
| Verifying the Environment is Healthy | Story 1.2 | Complete — health endpoints + `docker-compose ps` |
| Scenario Parameters | Story 1.3 | Complete |
| Reset to Clean Baseline | Story 1.5 | Complete |
| Running Tests | Epic 1 | Complete |
| Direct Database Inspection | Story 3.3 | Complete — SSMS + DB Browser steps |
| Viewing Sync Logs | Story 3.4 | Complete — docker logs + correlation ID tracing |
| Troubleshooting Unexpected Sync Outcomes | Story 3.5 | Complete — 5-step investigation flow |

**This story's only additions are:**
1. Expanding the Prerequisites section.
2. Adding a clone + .env creation preamble to Quick Start.
3. Adding home page navigation to Verifying the Environment is Healthy.

All other sections are COMPLETE. Do not rewrite or reorganize them.

### Gap 1: Prerequisites Section Missing Tools

**Current state:** The Prerequisites section lists `Docker Desktop` (with a link) and `A .env file in this folder`.

**Required additions (with versions/notes):**

| Tool | Version / Note |
|---|---|
| Docker Desktop | Latest stable (link to https://www.docker.com/products/docker-desktop/) |
| Git | Any recent version (for cloning the repository) |
| .NET 10 SDK | Required only if running or building outside Docker (optional for Docker-only workflow but listed for completeness) — https://dotnet.microsoft.com/download |
| Visual Studio 2022 (17.8+) or VS Code | Required for local debugging outside containers; Visual Studio is the primary dev environment [Source: project-context.md#Technology Stack & Versions] |
| SQL Server Management Studio (SSMS) | Optional — required for Direct Database Inspection of SQL Server; https://aka.ms/ssms |
| DB Browser for SQLite | Optional — required for Direct Database Inspection of SQLite; https://sqlitebrowser.org |

**Note on "optional" tools:** SSMS and DB Browser are already documented in the `Direct Database Inspection` section with installation links. The Prerequisites section should list them with `(optional — for database inspection)` so a developer running only the Docker scenario knows they can skip them initially. This avoids duplication of detail while ensuring discoverability.

### Gap 2: Quick Start Missing Clone and .env Steps

**Current state:** Quick Start begins directly with `docker-compose up --build`. A developer who just cloned the repo would not know they need to create a `.env` file before running any docker-compose command. Without the `.env` file, the `sqlserver` container will fail to start because `SA_PASSWORD` is undefined.

**Required preamble (insert before the docker-compose block):**

```markdown
### Before your first run

1. **Clone the repository** (skip if you already have it locally):
   ```bash
   git clone <repository-url>
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
```

**Rationale:** The `.env` file failure mode is one of the most common first-time developer errors — `docker-compose up` silently uses an empty `SA_PASSWORD`, which causes the SQL Server container to exit immediately with a configuration error. An explicit creation step prevents this.

### Gap 3: Health Verification Stops at /health — Home Pages Not Mentioned

**Current state:** The `Verifying the Environment is Healthy` section documents the `/health` endpoint table and `docker-compose ps`. This confirms processes are alive, but does NOT direct the developer to open the main application UI.

**Required addition (append to Verifying section):**

The addition should explain that health endpoint checks confirm container liveness, but the home page confirms end-to-end readiness (DB migrations applied, reference data seeded, UI rendering):

```markdown
### Accessing the Service Home Pages

Once all health checks return `{"status":"Healthy"}`, open the service home pages in a browser to confirm the UI and data layer are fully operational:

| Service | Home Page URL | What you should see |
|---|---|---|
| ServerService | http://localhost:5000 | jqGrid tables for Measurements, Buildings, Rooms, Surfaces, Cells, Users, and Sync Runs. On first run: 0 measurements, 5 users, 2 buildings, 4 rooms, 8 surfaces, 16 cells. |
| ClientService User 1 | http://localhost:5001 | jqGrid tables for Measurements (CRUD) and read-only reference grids. On first run: all tables empty until Pull Reference Data is triggered. Sync action buttons: Generate, Push, Pull, Reset Client DB, Pull Reference Data. |
| ClientService User 2–5 | http://localhost:5002 – 5005 | Same as User 1 (each backed by its own isolated SQLite database). |

> If ServerService's home page loads but shows 0 rows in all grids and no error, seeding may still be in progress — wait 5–10 seconds and refresh. If tables remain empty after 30 seconds, check the `serverservice-app` logs with `docker logs serverservice-app --tail 30` for seeding errors.
```

### Project Structure Notes

- The README file to modify is `MicroservicesSync/README.md` (in the `MicroservicesSync/` folder, at the root of the Docker-compose context).
- No changes to any other files in the solution.
- No changes to `docker-compose.yml`, `docker-compose.override.yml`, `.env` (that file should not be in the repo), `Dockerfile`, or any `*.csproj` / `*.cs` files.

### Alignment with Existing README Sections

The `Direct Database Inspection` section already mentions SSMS and DB Browser for SQLite with detailed usage steps. The new Prerequisites section should list these tools briefly; the deep-dive section remains authoritative for usage detail. No duplication or contradiction is introduced.

The `Reset to Clean Baseline` section already documents the reset API calls and expected entity counts. The new Quick Start section should reference this section (not duplicate it) for resetting before scenario runs.

### Epic 3 Retrospective Guidance for This Story

The Epic 3 retrospective ([Source: epic-3-retro-2026-03-12.md#Challenge 4]) explicitly flagged:
- "Story 4.1's definition of 'single-entry README' may need to be scoped as 'audit, integrate, and complete the README' rather than 'write from scratch.'"
- "The README is no longer an empty canvas."
- "Story 4.1 will need to audit, reorganize, and integrate rather than author from scratch."

This story is scoped accordingly: **three targeted additions** to the existing README, not a rewrite.

### References

- [Source: MicroservicesSync/README.md] — Full current README state
- [Source: _bmad-output/planning-artifacts/epics.md#Story 4.1] — Acceptance criteria origin
- [Source: _bmad-output/planning-artifacts/architecture.md#Infrastructure & Deployment] — Docker-compose hosting model, environment configuration
- [Source: _bmad-output/project-context.md#Technology Stack & Versions] — Visual Studio as primary dev environment, .NET 10
- [Source: _bmad-output/implementation-artifacts/epic-3-retro-2026-03-12.md#Challenge 4 and Next Epic Preview] — Context about README state entering Epic 4
- [Source: _bmad-output/implementation-artifacts/3-3-direct-database-inspection-support-for-diagnostics.md] — Existing SSMS + SQLite sections already in README
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-002] — 5×5×5 client topology → 5 ClientService home pages at 5001–5005

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

- `MicroservicesSync/README.md`
