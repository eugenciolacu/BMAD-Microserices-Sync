---
stepsCompleted:
  - step-01-init
  - step-02-discovery
  - step-02b-vision
  - step-02c-executive-summary
  - step-03-success
  - step-04-journeys
  - step-05-domain
  - step-06-innovation
  - step-07-project-type
  - step-08-scoping
  - step-09-functional
  - step-10-nonfunctional
  - step-11-polish
inputDocuments:
  - _bmad-output/planning-artifacts/product-brief-Microserices-Sync-2026-02-27.md
  - _bmad-output/project-context.md
  - _bmad-output/brainstorming/brainstorming-session-2026-02-26.md
documentCounts:
  brief: 1
  research: 0
  brainstorming: 1
  projectDocs: 0
workflowType: 'prd'
classification:
  projectType: developer_tool_experiment
  domain: general_software_infrastructure
  complexity: medium
  projectContext: greenfield
---

# Product Requirements Document - Microserices-Sync

**Author:** Eugen
**Date:** 2026-02-28

## Executive Summary

Microserices-Sync is a self-contained experiment that emulates offline/online synchronization between a HoloLens-like client application and a centralized source of truth, using two dockerized microservices (ClientService and ServerService). It de-risks future projects by validating a realistic multi-client sync pattern in advance, with a domain-relevant schema and explicit success metrics, instead of discovering sync issues late in a real engagement. The output is a working, repeatable reference that architects and developers can run, inspect, and treat as the default approach for similar future solutions.

The project focuses narrowly on synchronization correctness and data integrity, not on shipping a production system. Core proving scenarios include multiple ClientService instances generating measurements independently, pushing them to ServerService, and then converging so that every client and the server hold the same complete measurement set with no loss, duplication, or divergence. By packaging the entire setup in Docker, the experiment stays easy to hand over, run, and maintain, while remaining close enough in technology and schema to what a real HoloLens-plus-backend project will require.

### What Makes This Special

Microserices-Sync is explicitly designed as a developer tool and internal experiment: its primary output is a proven sync approach and reusable code, not end-user features. It uses a realistic data model and a multi-client architecture to exercise the exact kinds of edge cases that tend to break in production (concurrent client writes, server-side CRUD, and repeated sync cycles), with clear, objective success metrics for data integrity. The long-term goal is that parts of the implementation—such as data models, sync flows, and supporting infrastructure—can be carried into a real project with minimal changes.

The core differentiator is the "I tried it, I checked it, it works" guarantee. Instead of starting each new project with theoretical designs or generic samples, teams can base their sync strategy on a concrete, battle-tested experiment. This reduces uncertainty and exploration time when real project schedules are tight, letting developers and architects focus on product-specific concerns rather than re-solving sync fundamentals.

## Project Classification

- Project Type: Developer tool / internal experiment for sync design (implemented as backend/API microservices)
- Domain: General software / developer infrastructure
- Complexity: Medium
- Project Context: Greenfield experiment (no existing production system; intended to inform future HoloLens-style projects)

## Success Criteria

### User Success

- You (or another XR/AR/backend developer) can run the documented sync scenarios from the brief end-to-end and see that they complete without manual fixing or investigation.
- After a run, you can quickly verify that all ClientService instances and ServerService hold the same measurement set and that no records are missing, duplicated, or corrupted.
- When starting a real project, you can adopt the sync approach from Microserices-Sync with confidence instead of re-designing or re-researching sync from scratch.

### Business Success

- The first real project that needs multi-client HoloLens-style sync can reuse Microserices-Sync's patterns and, ideally, code with only minimal adaptation.
- The experiment measurably reduces time spent on sync-related exploration in that project (for example, architects and developers can decide on the sync approach within days instead of weeks).
- Sync-related production bugs or rework are significantly reduced compared to a "no experiment" baseline, because core edge cases were already exercised here.

### Technical Success

- For the core scenario where all clients push then all clients pull, the final measurement data on every ClientService instance is identical to the data on ServerService.
- The system supports repeated runs of the core scenarios from a clean state, always converging to identical datasets across all services.
- The implementation remains focused and minimal: only the elements required to prove sync correctness are present; non-essential features do not block or complicate the experiment.

### Measurable Outcomes

- In all configured core scenarios (e.g., the ones defined in the brief), the number and identities of measurements on ServerService and on each ClientService match exactly after sync (0 mismatches, 0 missing, 0 duplicates).
- The core scenarios can be run multiple times from a clean baseline with consistent, correct results on every run.
- For at least one real project, the team is able to reuse the experiment's sync design (and ideally code) rather than designing an entirely new approach.

### Success Criteria to FR/NFR Mapping

The table below maps representative success criteria to the primary requirements that implement them.

| Success Area           | Example Criteria                                                                                           | Primary FRs/NFRs                 |
|------------------------|-----------------------------------------------------------------------------------------------------------|----------------------------------|
| User Success           | Developer can run documented sync scenarios end-to-end and verify identical datasets across services.     | FR1–FR3, FR6–FR9, NFR1–NFR3      |
| Business Success       | First real project reuses this experiment with minimal adaptation and reduced exploration time.           | FR4–FR5, FR7–FR9, FR13–FR15      |
| Technical Success      | Core push/pull scenarios converge with no loss, duplication, or corruption across repeated runs.         | FR6–FR9, FR10–FR12, NFR1–NFR3    |
| Reliability & Repeatability | Standard scenarios can be run many times from a clean seeded state with consistent results.               | FR4–FR5, FR9, NFR2–NFR3          |
| Developer Experience   | New developers can bring up the environment, inspect data, and follow scenarios with minimal friction.    | FR1–FR3, FR10–FR15, NFR6–NFR7    |

## Product Scope

### MVP - Minimum Viable Product

- Two microservices (ClientService and ServerService) running in Docker, with the schema and endpoints needed to run the core sync scenarios from the brief.
- Implementation of the "all clients push, then all clients pull" scenario, with clear steps so a developer can execute and verify it.
- Minimal UI or inspection mechanisms sufficient to confirm that measurement data is identical across all services after sync.
- Documentation that explains how to run the scenarios and how to interpret the results for a future project.

### Growth Features (Post-MVP)

- Additional, more complex sync scenarios (different client counts, different measurement volumes, variations in timing/order).
- Better tooling for inspecting and comparing data across services (richer views, diagnostics, or reports).
- Optional additional conflict scenarios and strategies beyond the core correctness checks.

### Vision (Future)

- Microserices-Sync is the default internal reference for multi-client HoloLens-style sync: new projects start from it rather than from scratch.
- The experiment's patterns and code are mature enough that they can be treated as a small internal "sync starter kit" for future solutions.

## User Journeys

**Journey 1 – Developer: Running the Experiment and Trusting the Result (Primary, Success Path)**  
A developer hears that Microserices-Sync is the internal reference for multi-client sync. They clone the repo, run a single docker-compose command, wait for containers to come up, and open the simple UI or logs described in the README. They trigger the "all clients push, then all clients pull" scenario, watch it complete without errors, and then use the provided views or checks to confirm that every ClientService instance and ServerService show the same measurement set. They repeat this a couple of times from a clean state and see consistent, correct results. At the end, they feel confident enough to say "I tried it, I checked it, it works" and start sketching how to adapt the same pattern (and some of the code) into their real project.

**Journey 2 – Developer: Investigating a Sync Edge Case (Primary, Edge Case)**  
Later, the same or another developer wants to understand how the system behaves under a slightly different scenario (e.g., more clients, more measurements, or a different order of operations). They adjust configuration or use a documented variant scenario, re-run the environment, and use the same basic workflow to trigger sync and inspect results. When something looks unexpected, they rely on the experiment's logs and simple inspection tools to trace what happened without getting lost in infrastructure details. They either confirm that the behavior is still correct, or they discover a genuine edge case that can be fixed in the experiment before it ever appears in a production project.

**Journey 3 – Solution Architect: Evaluating and Reusing the Pattern**  
A solution architect preparing a new HoloLens-plus-backend project needs to decide on a sync approach. Instead of starting from a blank page, they review the Microserices-Sync README, architecture notes, and success metrics, then run the core scenario once to verify it behaves as described. They study how ClientService and ServerService are structured, how data flows, and how correctness is verified. Satisfied that the pattern is sound and matches the future project's needs, they reference Microserices-Sync in their architecture document and plan to reuse its models, endpoints, and sync flows as the baseline design, reducing uncertainty and exploration time.

**Journey 4 – QA / Validation: Proving It Still Works After Changes**  
A QA-minded engineer or developer changes something in the experiment (e.g., schema tweak, performance improvement, or refactoring). To ensure nothing broke, they follow the documented steps to bring up the environment and run the standard scenarios. They rely on the same checks developers use to confirm that all services converge to identical measurement sets. If a run fails or the data diverges, they can quickly spot that the experiment no longer meets its success criteria and roll back or fix the change, keeping Microserices-Sync trustworthy as a reference.

### Journey Requirements Summary

Across these journeys, the system needs to provide:

- A simple, reliable way to bring up all services (e.g., single docker-compose entrypoint) and reset to a known clean state.
- Clear, documented steps to run the core sync scenarios and any key variants.
- Straightforward mechanisms to compare or inspect data on ClientService instances and ServerService to confirm they match.
- Basic but trustworthy logging or diagnostics to help understand what happened during a run, especially when investigating edge cases or regressions.
- Documentation that explains how the experiment is structured, which scenarios exist, and how to interpret results for architectural and implementation decisions.

## Domain-Specific Requirements

For Microserices-Sync as general developer infrastructure, there are no special external regulatory or industry compliance constraints beyond standard good practices. The main domain expectation is that the experiment remains clear, reproducible, and maintainable for internal developers, with understandable structure, predictable behavior, and minimal operational overhead.

## Developer Tool Specific Requirements

### Project-Type Overview

Microserices-Sync is a .NET 10 ASP MVC–based developer experiment, delivered as a repo plus Docker configuration rather than as a packaged library. It is intended to be cloned and run directly by developers as an internal reference implementation for multi-client sync, not distributed via NuGet or other package managers.

### Technical Architecture Considerations

- Tech stack: .NET 10, ASP.NET MVC with Razor pages and jqGrid for simple data views and interaction.
- Services: at least two microservices (ClientService and ServerService), each running in Docker, with SQL Server for ServerService and SQLite for ClientService.
- Local tooling: developers are expected to have Visual Studio, SQL Server + SSMS, a SQLite viewer, and Docker installed to work comfortably with the project.
- Environment: docker-compose orchestrates the services; it must remain the primary, reliable way to bring up the full experiment and reset it to a clean state.

### Installation & Usage Model

- Distribution: no standalone packages or installers; usage is "clone the repository, configure if needed, run docker-compose and open the services in browser / tools."
- Configuration: any necessary configuration (e.g., connection strings, number of clients, data volumes) should be manageable via environment variables or clearly documented config files.
- Tooling assumptions: workflows and docs assume a Windows + Visual Studio developer environment, but should not be fragile if alternative IDEs are used.

### Documentation & Examples

- Single primary README that contains:
  - Quickstart instructions (clone, prerequisites, docker-compose commands, how to check that things are running).
  - High-level architecture notes (services, databases, main flows).
  - A clear scenario guide for the main sync experiments.
- Examples:
  - At least two standard sync scenarios with step-by-step instructions (e.g., core "all clients push then pull" scenario plus one variant).
  - Seed data provided via CSV or Excel so that the initial database state is predictable and can be easily reset for repeated runs.

### Implementation Considerations

- The project structure and scripts must make it easy for a new developer to run scenarios end-to-end without needing deep knowledge of Docker, SQL, or ASP.NET internals.
- Changes to schema or behavior should be accompanied by updated seed data and README instructions, so the experiment remains trustworthy and reproducible over time.

## Project Scoping & Phased Development

### MVP Strategy & Philosophy

**MVP Approach:** Learning/validation MVP focused on proving sync logic correctness in realistic multi-client scenarios.
**Resource Requirements:** Solo developer (you) with basic support from future reviewers; scope deliberately small to fit around other work.

### MVP Feature Set (Phase 1)

**Core User Journeys Supported:**
- Journey 1 – Developer: Running the Experiment and Trusting the Result.
- Journey 2 – Developer: Investigating a Sync Edge Case (one additional, clearly documented variant scenario).

**Must-Have Capabilities:**
- Docker-based environment with ClientService and ServerService, matching the planned real-project tech stack (ASP.NET MVC, SQL Server, SQLite, jqGrid).
- Implementation of the "all clients push, then all clients pull" scenario plus one configurable edge-case variant.
- Clear, repeatable way to reset to a clean seeded state (using CSV/Excel seed data).
- Logs and simple data views (jqGrid + DB tools) sufficient to verify that all ClientService instances and ServerService converge to the same measurement set.
- README with quickstart, architecture overview, and step-by-step instructions for the two core scenarios.

### Post-MVP Features

**Phase 2 (Post-MVP / Growth):**
- A simple audit page on ServerService showing recent sync runs (which client/user sent data, how many rows, high-level status).
- Additional scenario variants (different client counts, volumes, or timing patterns) beyond the first edge case.
- More convenient diagnostics for comparing datasets across services (e.g., summary counts, basic diff-style summaries).

**Phase 3 (Expansion / Future):**
- Richer reporting or visualization of sync history and conflicts.
- Additional conflict-resolution strategies and configuration options.
- Automation around seeding/cleanup and scenario orchestration (scripts or small tools) for quicker repeated runs.

### Risk Mitigation Strategy

**Technical Risks (main focus – sync logic):**
- Start with the simplest sync logic that can satisfy the defined success metrics (no loss, no duplication, convergence across all services).
- Implement the core scenario first, keep the data model and flows as transparent as possible, and add edge cases only after the base path is stable.
- Use repeatable runs plus logs and data checks as the primary way to detect and fix mistakes early.

**Market/Usage Risks:**
- Main risk is that the experiment ends up unused in future projects; mitigate by keeping it easy to run (one README, one docker-compose entrypoint) and closely aligned with the expected real-project architecture.

**Resource Risks:**
- If time is tighter than expected, de-scope Phase 2/3 items and focus on delivering a solid Phase 1 where the core scenario and one edge-case scenario work reliably and are well documented.

## Functional Requirements

Each functional requirement is traceable back to the Success Criteria and User Journeys sections above; the mapping table in Success Criteria highlights the primary links.

### Environment & Setup

- FR1: Developer can clone the Microserices-Sync repository and bring up all required services using a single documented command or short sequence (e.g., docker-compose plus minimal setup).
- FR2: Developer can verify that ClientService and ServerService are running correctly using a simple, documented check (status page, logs, or similar).
- FR3: Developer can adjust basic scenario parameters (such as number of clients and measurement volume) via configuration files or environment variables without changing code.

### Data Seeding & Reset

- FR4: Developer can initialize all databases to a known baseline state using provided seed data (CSV/Excel) and documented steps.
- FR5: Developer can reset the environment to the same clean baseline between runs so that repeated experiments start from a consistent state.

### Core Sync Scenarios

- FR6: Developer can run the core sync scenario where multiple ClientService instances each generate measurements, push them to ServerService, and then pull the consolidated measurements back.
- FR7: Developer can run at least one additional, clearly documented edge-case scenario variant (for example with different client counts or measurement patterns).
- FR8: After a scenario run completes, the system ensures that all ClientService instances and ServerService hold the same set of measurements (no missing, duplicated, or corrupted records), verifiable using the provided tools.
- FR9: Developer can repeat the defined scenarios multiple times from a clean baseline and observe consistent, correct results.

### Diagnostics & Audit

- FR10: Developer can view a simple summary of recent sync runs on ServerService, including which user sent data and how many measurement records were processed per run.
- FR11: Developer can inspect measurement data on both ClientService and ServerService via jqGrid views and/or database tools to manually confirm data correctness.
- FR12: Developer can access logs or other basic diagnostics from both services to help investigate unexpected sync behavior or edge cases.

### Documentation & Guidance

- FR13: Developer can follow a single README that provides prerequisites, quickstart steps, and commands to bring the system up and run the core scenarios.
- FR14: Developer can follow a scenario guide (within the same README) that describes step-by-step how to execute the core and edge-case sync scenarios and how to check their outcomes.
- FR15: Developer and architect can read concise architecture notes (also in README) that explain service responsibilities, data flows, and how the sync logic is structured so they can reason about reusing the pattern.

## Non-Functional Requirements

These non-functional requirements support the measurable outcomes defined in Success Criteria (especially performance, reliability, and environment expectations) and ensure the experiment remains repeatable and trustworthy over time.

### Performance

- NFR1: Core sync scenarios (including the standard 5-clients × 10-measurements case) complete within a few minutes on a typical developer laptop, without UI actions feeling blocked or unresponsive.

### Reliability & Repeatability

- NFR2: The standard scenario with 5 ClientService instances, each generating 10 measurements, can be run 10 consecutive times from a clean seeded state without random failures.
- NFR3: For all 10 runs in the standard scenario above, ServerService and all ClientService instances converge to identical measurement datasets (no missing, duplicated, or corrupted records).

### Security

- NFR4: The system uses only non-sensitive, artificial data, and does not require production-grade authentication or authorization mechanisms.
- NFR5: All database interactions are implemented in a way that prevents SQL injection (for example, using parameterized queries or equivalent ORM protections).

### Environment & Integration

- NFR6: The experiment runs cleanly with Docker, SQL Server, SQLite, and Visual Studio on a typical developer laptop, without requiring external cloud services.
- NFR7: Required tooling and version expectations (Docker, SQL Server, SQLite, Visual Studio) are clearly documented so developers can set up a compatible environment.

