---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/product-brief-Microserices-Sync-2026-02-27.md
---

# Microserices-Sync - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Microserices-Sync, decomposing the requirements from the PRD and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Developer can clone the Microserices-Sync repository and bring up all required services using a single documented command or short sequence (for example, docker-compose plus minimal setup).
FR2: Developer can verify that ClientService and ServerService are running correctly using a simple, documented check (such as a status page or logs).
FR3: Developer can adjust basic scenario parameters (such as number of clients and measurement volume) via configuration files or environment variables without changing code.
FR4: Developer can initialize all databases to a known baseline state using provided seed data (CSV/Excel) and documented steps.
FR5: Developer can reset the environment to the same clean baseline between runs so that repeated experiments start from a consistent state.
FR6: Developer can run the core sync scenario where multiple ClientService instances each generate measurements, push them to ServerService, and then pull the consolidated measurements back.
FR7: Developer can run at least one additional, clearly documented edge-case scenario variant (for example, with different client counts or measurement patterns).
FR8: After a scenario run completes, the system ensures that all ClientService instances and ServerService hold the same set of measurements (no missing, duplicated, or corrupted records), verifiable using the provided tools.
FR9: Developer can repeat the defined scenarios multiple times from a clean baseline and observe consistent, correct results.
FR10: Developer can view a simple summary of recent sync runs on ServerService, including which user sent data and how many measurement records were processed per run.
FR11: Developer can inspect measurement data on both ClientService and ServerService via jqGrid views and/or database tools to manually confirm data correctness.
FR12: Developer can access logs or other basic diagnostics from both services to help investigate unexpected sync behavior or edge cases.
FR13: Developer can follow a single README that provides prerequisites, quickstart steps, and commands to bring the system up and run the core scenarios.
FR14: Developer can follow a scenario guide (within the same README) that describes step-by-step how to execute the core and edge-case sync scenarios and how to check their outcomes.
FR15: Developer and architect can read concise architecture notes (also in the README) that explain service responsibilities, data flows, and how the sync logic is structured so they can reason about reusing the pattern.

### NonFunctional Requirements

NFR1: Core sync scenarios (including the standard 5-clients × 10-measurements case) complete within a few minutes on a typical developer laptop, without UI actions feeling blocked or unresponsive.
NFR2: The standard scenario with 5 ClientService instances, each generating 10 measurements, can be run 10 consecutive times from a clean seeded state without random failures.
NFR3: For all 10 runs of the standard scenario, ServerService and all ClientService instances converge to identical measurement datasets (no missing, duplicated, or corrupted records).
NFR4: The system uses only non-sensitive, artificial data and does not require production-grade authentication or authorization mechanisms.
NFR5: All database interactions are implemented in a way that prevents SQL injection (for example, using parameterized queries or equivalent ORM protections).
NFR6: The experiment runs cleanly with Docker, SQL Server, SQLite, and Visual Studio on a typical developer laptop, without requiring external cloud services.
NFR7: Required tooling and version expectations (Docker, SQL Server, SQLite, Visual Studio) are clearly documented so developers can set up a compatible environment.

### Additional Requirements

- Use two ASP.NET Core MVC microservices, ServerService and ClientService, plus shared class libraries (Sync.Domain, Sync.Application, Sync.Infrastructure) in a single solution following clean/onion architecture.
- Provide a single HomeController with an Index action per service that hosts jqGrid-based tables for all relevant entities, plus per-entity API controllers exposing jqGrid-friendly CRUD endpoints.
- Implement EF Core code-first with two DbContexts (ServerDbContext for SQL Server and ClientDbContext for SQLite) that share Guid-based domain entities defined in Sync.Domain.
- Include concurrency tokens for mutable entities (rowversion on SQL Server and a numeric token on SQLite) and use them in sync conflict detection and resolution.
- Seed ServerService with all reference data tables except Measurements on first startup, while each ClientService database starts empty and performs an initial reference-data pull from ServerService.
- Provide a "clean & seed" reset mechanism that reseeds ServerService (all reference tables except Measurements) and clears all ClientService databases, restoring a known baseline.
- Expose sync endpoints on ServerService (for example, POST /api/v1/sync/measurements/push and GET /api/v1/sync/measurements/pull) and configure ClientService to call them via a SERVER_BASE_URL setting.
- Restrict ClientService to full CRUD only for Measurements while keeping other entities read-only, and allow ServerService full CRUD for all entities.
- Run the entire environment via a local docker-compose file defining ServerService, multiple ClientService containers, and a sqlserver container on a private Docker network.
- Persist SQL Server data in a named Docker volume and each ClientService SQLite database and logs in dedicated per-container volumes or bind mounts.
- Use environment variables and standard ASP.NET Core configuration layering to provide connection strings, SQLite paths, and the server base URL.
- Fix the MVP topology to exactly five seeded users and five ClientService containers, each mapped to a specific user via a ClientIdentity__UserId environment variable and its own database and log volumes.
- Ensure observability via application logs, jqGrid views, and direct database inspection tools (such as SQL Server Management Studio and a SQLite viewer) rather than full APM/monitoring stacks.
- Treat Microserices-Sync as a pre-production experiment whose main deliverable is a reusable, understandable sync pattern and reference implementation, not a production-ready system.
- Implement sync push and pull processing as configurable-size batches (for example, default 5 measurements per batch) executed within a single database transaction per sync operation, so that all batches for that push or pull either commit together or the entire operation rolls back on error.

### FR Coverage Map

### FR Coverage Map

FR1: Epic 1 – Environment & one-command bring-up of all services.
FR2: Epic 1 – Verifying that ClientService and ServerService are running correctly.
FR3: Epic 1 – Adjusting client count and measurement volume via configuration.
FR4: Epic 1 – Initializing all databases to a known baseline state from seed data.
FR5: Epic 1 – Resetting the environment back to the clean baseline between runs.

FR6: Epic 2 – Running the core multi-client push/pull sync scenario.
FR7: Epic 2 – Running at least one additional, clearly documented edge-case scenario.
FR8: Epic 2 – Ensuring all services converge to the same measurement set after a run.
FR9: Epic 2 – Repeating scenarios from a clean baseline with consistent, correct results.

FR10: Epic 3 – Viewing a simple summary of recent sync runs on ServerService.
FR11: Epic 3 – Inspecting measurement data on both services via jqGrid/DB tools.
FR12: Epic 3 – Accessing logs/diagnostics to investigate sync behavior and edge cases.

FR13: Epic 4 – Single README with prerequisites, quickstart, and commands.
FR14: Epic 4 – Scenario guide describing how to execute and validate core/edge scenarios.
FR15: Epic 4 – Architecture notes explaining responsibilities, flows, and sync structure.

## Epic List

### Epic 1: Environment, Seeding, and Reset Baseline
Enable developers to clone the repo, bring up all services via a simple command, configure scenario parameters, and initialize/reset all databases to a known baseline so experiments start from a consistent state.
**FRs covered:** FR1, FR2, FR3, FR4, FR5

### Epic 2: Multi-Client Sync Scenarios
Enable developers to run core and edge-case multi-client sync scenarios from a clean baseline and reliably converge all ClientService instances and ServerService to the same measurement dataset across repeated runs.
**FRs covered:** FR6, FR7, FR8, FR9

### Epic 3: Sync Diagnostics and Audit
Enable developers to see what happened during sync: view summaries of sync runs, inspect data on both services, and use logs/diagnostics to investigate unexpected behavior or edge cases.
**FRs covered:** FR10, FR11, FR12

### Epic 4: Developer Documentation and Guidance
Enable developers and architects to quickly understand how to install, run, and reason about Microserices-Sync through a single README that covers quickstart, scenarios, and architecture notes.
**FRs covered:** FR13, FR14, FR15

### Epic 5: Polishing
Addresses targeted defects and incremental improvements discovered after the initial implementation (Epics 1–4). Intended for fixes, workflow corrections, and small enhancements — not originally planned scope.
**Stories:** 5.1 (Reset to Baseline completeness), 5.2 (Serilog file logging). Additional stories may be added as further improvements are identified.

## Epic 1: Environment, Seeding, and Reset Baseline

Enable developers to clone the repo, bring up all services via a simple command, configure scenario parameters, and initialize/reset all databases to a known baseline so experiments start from a consistent state.

### Story 1.1: Single-Command Environment Startup

As a developer,
I want to start all Microserices-Sync services with a single documented command,
So that I can quickly bring up the full experiment environment without manual wiring.

**Acceptance Criteria:**

**Given** I have cloned the repository and installed the documented prerequisites
**When** I run the documented startup command (for example, `docker-compose up`)
**Then** ServerService, all configured ClientService instances, and SQL Server containers start successfully.

**Given** the containers are running
**When** I open the documented URLs for ServerService and each ClientService
**Then** I can reach their home pages without HTTP or container errors.

**Given** the environment has started
**When** I stop and restart it using the documented command
**Then** it starts reliably again without requiring manual container cleanup.

### Story 1.2: Service Health and Status Verification

As a developer,
I want a simple way to verify that ClientService and ServerService are healthy,
So that I can confirm the environment is ready before running scenarios.

**Acceptance Criteria:**

**Given** all services are started
**When** I hit the documented health/status endpoints or check the designated status indicators in the UI
**Then** each running service reports a healthy status.

**Given** a service is intentionally stopped or misconfigured
**When** I use the same health/status check
**Then** I can clearly see that the service is unhealthy or unavailable.

**Given** the health checks are documented
**When** a new developer follows the README instructions
**Then** they can determine within minutes whether the environment is healthy.

### Story 1.3: Configurable Scenario Parameters

As a developer,
I want to configure core scenario parameters without changing code,
So that I can vary client counts and measurement volumes easily.

**Acceptance Criteria:**

**Given** the environment is configured via documented config files or environment variables
**When** I change the configured number of ClientService instances
**Then** the next environment startup uses the new client count as expected.

**Given** measurement volume per client is configurable
**When** I update the documented configuration value for measurements per client
**Then** sync scenarios use the new volume without requiring recompilation.

**Given** these parameters are documented in the README
**When** a new developer follows the instructions
**Then** they can adjust scenario parameters successfully on their first attempt.

### Story 1.4: Initial Database Seeding from Baseline Data

As a developer,
I want all databases to be initialized from known baseline seed data,
So that every experiment run starts from the same reference state.

**Acceptance Criteria:**

**Given** I run the documented initial seed or startup process from a clean environment
**When** ServerService starts for the first time
**Then** its SQL Server database is created and populated with all reference data tables except Measurements, using the provided seed files.

**Given** ClientService databases start empty
**When** I run the documented initial reference-data sync or seed flow
**Then** each ClientService receives the same reference data set as ServerService (excluding Measurements).

**Given** seeding has completed successfully
**When** I inspect the relevant tables in SQL Server and each SQLite database
**Then** their structure and baseline contents match the documented expectations.

### Story 1.5: Reset Environment to Clean Baseline

As a developer,
I want a documented way to reset all databases to the clean baseline,
So that I can repeat experiments from the same starting point.

**Acceptance Criteria:**

**Given** the system has been used and data has changed
**When** I execute the documented "clean & seed" or reset command/flow
**Then** ServerService is reseeded to the original reference-data-only state (all tables except Measurements) and all ClientService databases are cleared.

**Given** the reset has finished
**When** I start the environment and (if required) run the initial reference-data sync
**Then** all databases return to the same baseline state as a fresh install.

**Given** the reset operation is documented
**When** a new developer follows the steps
**Then** they can reliably restore the environment to the clean baseline without manual database operations.

## Epic 2: Multi-Client Sync Scenarios

Enable developers to run core and edge-case multi-client sync scenarios from a clean baseline and reliably converge all ClientService instances and ServerService to the same measurement dataset across repeated runs, using batched operations inside a single transaction per push/pull.

### Story 2.1: Client-Side Measurement Generation per Instance

As a developer,
I want each ClientService instance to generate its own configurable set of measurements,
So that I can emulate independent client activity before sync.

**Acceptance Criteria:**

**Given** a clean baseline state and configured measurement-per-client settings
**When** I trigger the documented measurement generation flow on each ClientService
**Then** each instance creates the expected number of new measurements tagged with its own client/user identity.

**Given** measurements have been generated
**When** I inspect each ClientService database and Measurements grid
**Then** I see only that client’s locally generated measurements, with correct metadata (user, timestamps, IDs).

**Given** I adjust the configured measurements-per-client value
**When** I rerun the generation flow from a clean baseline
**Then** each client generates the new configured volume.

### Story 2.2: Transactional, Batched Server-Side Measurement Push

As a developer,
I want ClientService instances to push their local measurements to ServerService in batches inside a single transaction,
So that either all pushed measurements for that client are applied or none are, even for larger datasets.

**Acceptance Criteria:**

**Given** multiple ClientService instances have generated local measurements
**When** I trigger the documented push operation from a client
**Then** the client sends its measurements to ServerService in configurable-size batches (for example, 5 records per batch) while ServerService processes all batches for that push inside a single database transaction.

**Given** a push operation completes successfully
**When** I inspect the ServerService database and Measurements grid
**Then** all measurements sent by that client for that push are present exactly once, with no missing or partially applied batches.

**Given** an error occurs while applying any batch within a push (for example, a validation or DB error)
**When** the push operation ends
**Then** the transaction is rolled back and none of that client’s new measurements from that push are persisted, and the failure is visible in logs or client-facing feedback.

### Story 2.3: Transactional, Batched Client-Side Pull of Consolidated Measurements

As a developer,
I want each ClientService instance to pull the consolidated measurement set from ServerService in batches inside a single transaction,
So that each client either fully converges to the server dataset or not at all for that pull.

**Acceptance Criteria:**

**Given** ServerService holds the consolidated measurements after successful pushes
**When** I trigger the documented pull operation on a client
**Then** the client retrieves measurements from ServerService in configurable-size batches (for example, 5 records per batch) and applies all batches for that pull within a single local database transaction.

**Given** a pull operation completes successfully
**When** I compare measurement counts and identifiers across ServerService and that ClientService
**Then** they match exactly, with no missing, extra, or duplicate records.

**Given** an error occurs while applying any batch during a pull
**When** the pull operation ends
**Then** the local transaction is rolled back and the client’s Measurements table remains unchanged by that pull attempt.

### Story 2.4: Edge-Case Multi-Client Scenario Variant with Batching

As a developer,
I want to run at least one clearly documented edge-case multi-client scenario using batched, transactional sync,
So that I can validate behavior under different timing or volume conditions with all-or-nothing guarantees.

**Acceptance Criteria:**

**Given** the standard scenario is implemented
**When** I configure and run the documented edge-case variant (for example, different client count, staggered pushes, or larger per-client volumes) with batched push and pull enabled
**Then** the scenario completes without unhandled errors.

**Given** the edge-case variant has completed successfully
When I inspect measurement data on ServerService and all involved ClientService instances
Then each client and the server converge to the same dataset, confirming that batched, transactional sync still prevents loss or duplication.

**Given** the edge-case scenario is documented
**When** a new developer follows the instructions
**Then** they can reproduce the same steps, including the batching configuration, and verify convergence.

### Story 2.5: Repeatable Transactional Sync Runs from Clean Baseline

As a developer,
I want to repeat the core and edge-case scenarios multiple times from a clean baseline with transactional, batched sync,
So that I can build confidence in reliability and repeatability for larger effective datasets.

**Acceptance Criteria:**

**Given** the "clean & seed" baseline reset is available
**When** I run the documented cycle (reset → generate → transactional batched push → transactional batched pull → verify) multiple times
**Then** each run completes successfully without unexplained failures.

**Given** I execute the standard scenario (for example, 5 clients × 10 measurements) 10 times from a clean baseline with batching enabled
**When** I compare datasets after each run
**Then** ServerService and all ClientService instances converge to identical datasets every time.

**Given** I run the documented edge-case variant repeatedly from a clean baseline with batching enabled
**When** I inspect results over several runs
**Then** I observe consistent, correct convergence behavior that matches the documented expectations.

## Epic 3: Sync Diagnostics and Audit

Enable developers to see what happened during sync: view summaries of sync runs, inspect data on both services, and use logs and diagnostics to investigate unexpected behavior or edge cases.

### Story 3.1: Server-Side Sync Run Summary View

As a developer,
I want a simple summary view of recent sync runs on ServerService,
So that I can quickly see which clients synced, when, and how many measurements were processed.

**Acceptance Criteria:**

**Given** ServerService has processed at least one sync push or pull
**When** I open the documented sync summary view on ServerService
**Then** I see a list of recent sync runs including at least timestamp, client/user identity, run type (push or pull), and number of measurements processed.

**Given** multiple clients have synced over time
**When** I sort or filter the summary view by client/user or date
**Then** I can focus on the subset of runs relevant to my investigation.

**Given** a sync run failed or partially succeeded
**When** I open the summary view
**Then** the failed run is clearly marked with an error or warning status and links or guidance to inspect logs for details.

### Story 3.2: Measurement Inspection via jqGrid on Both Services

As a developer,
I want jqGrid-based views of measurements on both ServerService and ClientService,
So that I can manually inspect and compare measurement records after sync.

**Acceptance Criteria:**

**Given** measurements exist on ServerService and on one or more ClientService instances
**When** I open the Measurements grid on ServerService
Then I can page, sort, and filter measurements, and each row shows key fields (IDs, client/user, timestamps, and values) needed for comparison.

**Given** I open the Measurements grid on a ClientService instance
**When** I apply the same sort and filter conditions as on ServerService
**Then** the set of visible measurement rows matches what I see on ServerService after a successful sync.

**Given** I need to investigate a specific measurement
**When** I search or filter by its unique identifier or client/user
**Then** I can locate the same record on both ServerService and ClientService to confirm it is present and aligned.

### Story 3.3: Direct Database Inspection Support for Diagnostics

As a developer,
I want the environment to support direct database inspection on both SQL Server and SQLite,
So that I can verify data correctness beyond the UI when diagnosing sync issues.

**Acceptance Criteria:**

**Given** the environment is running
**When** I use SQL Server Management Studio to connect to the ServerService database using the documented connection details
**Then** I can query the Measurements and reference tables and see data that matches what is displayed in the ServerService UI.

**Given** I use a SQLite viewer against a ClientService database file using the documented path
**When** I query the Measurements table
**Then** I see rows and counts that match the ClientService Measurements jqGrid and expected sync results.

**Given** a new developer follows the README instructions for DB inspection
**When** they connect to the databases for the first time
**Then** they can successfully locate the relevant tables and confirm that data matches the documented scenarios.

### Story 3.4: Application Logging for Sync Operations

As a developer,
I want structured logs for sync operations on both services,
So that I can trace what happened during a sync run and understand any failures.

**Acceptance Criteria:**

**Given** a sync push or pull is executed
**When** I inspect the ServerService logs via the documented location (for example, container logs or mounted log files)
**Then** I see entries that include at least client/user identity, run ID or correlation ID, number of measurements processed, and success or failure status.

**Given** a sync operation fails on either service
**When** I review the corresponding logs
**Then** I can see error messages or stack traces with enough context (run ID, client identity, operation type) to start diagnosing the problem.

**Given** I trigger multiple sync runs in sequence
**When** I filter logs by correlation ID or timestamp range
**Then** I can distinguish between different runs and follow the flow of a single run end to end.

### Story 3.5: Troubleshooting Flow for Unexpected Sync Outcomes

As a developer,
I want a documented troubleshooting flow that uses the summary view, grids, and logs together,
So that I can systematically investigate and resolve unexpected sync outcomes.

**Acceptance Criteria:**

**Given** a sync run appears to produce missing, extra, or duplicated measurements
**When** I follow the documented troubleshooting steps in the README or diagnostics section
**Then** I am guided to check the sync summary view, relevant jqGrid views, and corresponding logs in a specific order.

**Given** I complete the documented troubleshooting steps
**When** I re-run the scenario after making any recommended fixes or configuration changes
**Then** I can confirm via the same checks that ServerService and all ClientService instances now converge correctly.

**Given** a new developer encounters a sync anomaly for the first time
**When** they follow the troubleshooting flow
**Then** they can independently gather the right evidence (summaries, grid views, logs, and DB queries) to either resolve the issue or escalate it with clear, reproducible details.

## Epic 4: Developer Documentation and Guidance

Enable developers and architects to quickly understand how to install, run, and reason about Microserices-Sync through a single README that covers quickstart, scenarios, and architecture notes.

### Story 4.1: Single-Entry README with Prerequisites and Quickstart

As a developer,
I want a single README that lists prerequisites and quickstart steps,
So that I can bring up the environment and run the core scenario without hunting through multiple documents.

**Acceptance Criteria:**

**Given** I open the repository root
**When** I open the main README file
**Then** I see a clear prerequisites section that lists required tools and versions (for example, Docker, SQL Server/containers, SQLite viewer, and Visual Studio) aligned with the PRD and architecture.

**Given** I have the prerequisites installed
**When** I follow the quickstart section step by step
**Then** I can clone the repo, configure any required environment variables, and start all services using the documented single command or short sequence.

**Given** the environment has started successfully
**When** I follow the remaining quickstart steps
**Then** I can reach the ServerService and ClientService home pages and confirm basic health using the documented checks.

### Story 4.2: Scenario Guide for Core and Edge-Case Sync Runs

As a developer,
I want a scenario guide embedded in the README for the core and edge-case sync runs,
So that I know exactly which steps to execute and how to verify outcomes.

**Acceptance Criteria:**

**Given** I scroll to the scenarios section of the README
**When** I read the instructions for the core multi-client scenario (for example, 5 clients × 10 measurements)
**Then** I see a numbered sequence that covers clean baseline reset, measurement generation, transactional batched push, transactional batched pull, and verification steps.

**Given** I follow the core scenario instructions from start to finish
**When** I compare measurement datasets on ServerService and all ClientService instances using the documented grids and tools
**Then** I can confirm that all services converge to the same dataset if the system is working correctly.

**Given** the README also documents at least one edge-case scenario variant
**When** I follow the edge-case instructions (including any special configuration or timing)
**Then** I can reproduce the variant run and verify convergence or expected behavior using the same verification guidance.

### Story 4.3: Architecture Overview Notes for Reuse

As a solution architect or senior developer,
I want concise architecture notes in the README,
So that I can quickly understand service responsibilities, data flows, and sync structure and decide how to reuse the pattern.

**Acceptance Criteria:**

**Given** I open the architecture overview section of the README
**When** I read through it once
**Then** I understand the high-level topology (ServerService, multiple ClientService instances, SQL Server, SQLite), key domain entities, and how data flows during sync.

**Given** I am evaluating Microserices-Sync for reuse in a real project
**When** I consult the architecture notes
**Then** I see a clear mapping from PRD concepts (FRs and NFRs) to the implemented structure, including where sync orchestration, seeding/reset, and diagnostics live.

**Given** a new architect or developer joins the team
**When** they read the README’s architecture section and follow the links to deeper documents (such as the PRD and architecture decision doc)
**Then** they can explain back the core design and sync pattern within a short onboarding session without needing to read the entire codebase first.
## Epic 5: Polishing

Addresses targeted defects and incremental improvements discovered after the initial implementation of Epics 1–4. Epic 5 is the designated vehicle for fixes, workflow corrections, and small enhancements that arise during or after main sprint execution — not planned scope, but necessary for correctness and operational quality.

### Story 5.1: Update Reset to Baseline Completeness

As a developer,
I want the "Reset to Baseline" button on ServerService and the "Reset Client DB" button on ClientService to fully clear all data from their respective databases,
So that the reset operation leaves no stale data behind regardless of which tables were added after the initial reset implementation.

**Acceptance Criteria:**

**Given** the system has accumulated sync history (SyncRuns records) and measurement data
**When** I click "Reset to Baseline" on ServerService
**Then** the ServerService SQL Server database is fully cleared — including the SyncRuns table — and then reseeded with reference data, leaving zero orphaned records in any table.

**Given** a ClientService instance has accumulated data
**When** I click "Reset Client DB" on that ClientService instance
**Then** all tables in the ClientService SQLite database are fully cleared (ClientService does not have a SyncRuns table; the existing delete sequence covers all its tables).

**Given** new tables are added to the schema in a future story
**When** the developer implements the new entity
**Then** the same story must include an update to `DatabaseResetter` adding the new table to the correct FK-safe deletion order so this pattern is maintained.

### Story 5.2: Serilog File Logging

As a developer,
I want both ServerService and ClientService to write structured logs to rolling daily files in the mounted `/app/logs` Docker volume,
So that I can inspect persistent log history after a container restart and correlate log entries with sync operations across multiple runs.

**Acceptance Criteria:**

**Given** the environment is running via docker-compose
**When** a sync operation, reset, startup event, or any log-worthy activity occurs on either service
**Then** log entries are written to a rolling daily file under the service container's `/app/logs` volume in addition to the existing console output.

**Given** I inspect the log files written to a Docker volume (e.g., by mounting it locally or using `docker cp`)
**When** I open a log file for ServerService or any ClientService instance
**Then** I see timestamped, levelled, structured entries including source context, message, and exception details on failure.

**Given** Serilog file logging is configured
**When** the service is also producing output to stdout
**Then** `docker-compose logs` still shows full console output — the Serilog Console sink is active alongside the file sink.