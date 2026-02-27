---
stepsCompleted: [1, 2, 3, 4, 5]
inputDocuments:
  - _bmad-output/brainstorming/brainstorming-session-2026-02-26.md
  - _bmad-output/project-context.md
date: 2026-02-27
author: Eugen
---

# Product Brief: Microserices-Sync

## Executive Summary

Microserices-Sync is an experimental project to explore and validate a realistic offline/online data synchronization pattern between multiple client applications and a centralized backend. It emulates several HoloLens-like clients (ClientService instances) synchronizing measurement data with a central persistence service (ServerService), using a schema that is close to what a real project might require.

The purpose is not to ship a production system, but to reduce risk for future work by answering a focused question in advance: "How do we implement this kind of multi-client sync without losing or corrupting data?" Success is defined very practically - a working reference implementation that you and other developers can both run and understand, with clear documentation of the key sync decisions, trade-offs, and patterns used.

By building and testing Microserices-Sync now, architects and developers gain a concrete, runnable reference they can adapt later, instead of relying only on theory or generic samples when a real project or tender appears.

---

## Core Vision

### Problem Statement

When architects and developers design offline/online synchronization between several clients and a central backend, they often have to make key decisions without a realistic, domain-relevant reference implementation. Trying to design and validate sync behavior directly inside a production project is risky, because subtle sync and data-loss issues typically surface late, when they are most expensive to fix. Future team members and less experienced developers also need a concrete example they can study and extend, not just abstract architecture notes.

Microserices-Sync addresses this gap by acting as an experimental project: a safe environment to explore how multiple client services can synchronize measurements with a central server database using a schema that is close to the intended real-world model.

### Problem Impact

Without a dedicated experiment like Microserices-Sync:

- The risk of data loss or silent data corruption during sync remains high, especially when multiple client instances push changes independently.
- Architects and developers must rely on fragmented examples, documentation, or samples that don't match their actual domain or constraints.
- Edge cases around conflicts, timing, and multi-client behavior may only be discovered late in a real project.
- When preparing or bidding for a real project, uncertainty around the sync approach weakens confidence and increases delivery and estimation risk.

A poorly validated sync design can lead to lost measurements, inconsistent datasets across clients and server, and significant rework in both backend and client code.

### Why Existing Solutions Fall Short

Existing documentation, frameworks, and sample projects for synchronization tend to be either very generic or targeted at different patterns than "several field devices synchronizing measurements with a central service." They usually do not:

- Use a schema that resembles entities like Buildings, Rooms, Surfaces, Cells, Measurements, and Users.
- Emulate multiple independent client instances synchronizing against a shared server.
- Focus explicitly on verifying that no data is lost when many clients push and then pull data in realistic flows.

This leaves a gap between abstract guidance and a concrete, trustworthy implementation that feels representative of the future project.

### Proposed Solution

Microserices-Sync is built around two microservices:

- **ServerService** — central persistence layer and source of truth.
- **ClientService** — emulates a HoloLens-like client application.

The experiment focuses on Measurements as the primary write path from clients. A key proving scenario is:

1. Run five ClientService instances.
2. Each instance generates ten measurement records locally.
3. Each client pushes its measurements to ServerService.
4. Each client then pulls the updated measurement set from ServerService.

After synchronization, all ClientService instances should hold exactly the same set of measurements as ServerService, demonstrating consistent, lossless sync behavior across multiple clients.

The system is containerized and uses technologies such as .NET 10, SQL Server, and SQLite - close to what a future real project might use, while remaining manageable for experimentation. The project should expose sync steps and outcomes in an inspectable way (logs, simple views, or reports), so someone new to the codebase can trace how data moves and why it remains consistent.

### Key Differentiators

- **Experiment-first framing**: The project is explicitly defined as a pre-production experiment to de-risk future work, not as a finished product.
- **Concrete success scenario**: Success is measured against clear scenarios (for example, 5 clients with 10 measurements each, all successfully round-tripped) rather than vague "it seems fine" impressions.
- **Domain-relevant schema**: The data model (Buildings, Rooms, Surfaces, Cells, Measurements, Users) is close to the expected real solution, making lessons more transferable.
- **Multi-client emphasis**: Design and tests explicitly target the "many clients / one server" pattern, not a simplified single-client demo.

## Target Users

### Primary Users

**XR/AR Developer (Experiment Owner)**  
- Role & context: XR/AR engineer responsible for implementing HoloLens-side behavior in future projects. Often asked to figure out "how this could work" before a real project is fully funded.  
- Problem experience: Needs to design and validate multi-client offline/online sync between several HoloLens devices (ClientService) and a centralized backend (ServerService), but currently has no concrete, domain-relevant reference implementation. Worried about data loss and subtle sync bugs appearing late in a real engagement.  
- Goals & motivations: Wants a working, understandable reference solution that proves measurements from multiple devices can be synchronized without losing or corrupting records, and that can be adapted quickly when the company wins a tender.  
- Success: Can point to Microserices-Sync as "the way we do sync," reuse patterns, and feel confident that the core scenarios (e.g., 5 clients × 10 measurements each) are already solved.

**Backend Developer (Future Collaborator)**  
- Role & context: Backend engineer responsible for APIs, persistence, and sync logic on the server side. May join later when a real project is greenlit.  
- Problem experience: Needs to implement robust server-side sync endpoints and data handling without guessing about how clients will behave. Risks late-discovered bugs, inconsistent data, or performance issues if starting from scratch.  
- Goals & motivations: Wants a clear example of server-side models, endpoints, and sync flows that already handle multi-client measurement uploads and downloads reliably.  
- Success: Can reuse Microserices-Sync patterns and code structure as a starting point, instead of inventing everything under time pressure.

**Solution Architect (System-Level Owner)**  
- Role & context: Designs the overall architecture for a real project that will include multiple HoloLens devices and a centralized storage application. Needs to choose patterns and technologies that are realistic for the team.  
- Problem experience: Must make architectural decisions about sync, databases, and services without strong evidence of how they behave in practice with multiple clients. Data-loss risk is a major concern.  
- Goals & motivations: Wants empirical confidence that the chosen pattern (two services, sync endpoints, change flow) works as expected and can scale to the envisioned scenarios.  
- Success: Uses Microserices-Sync as architectural evidence when designing and justifying the real solution to stakeholders.

### Secondary Users

**Construction/Demolition Worker (HoloLens Device User)**  
- Role & context: Field worker using a HoloLens-like device to capture or view measurements on site.  
- Relationship to experiment: Not a direct user of Microserices-Sync, but their future experience depends on sync working reliably behind the scenes. The experiment helps ensure their measurements are not lost and are consistent across devices.

**Manager / Planner (Central Data Owner)**  
- Role & context: Person responsible for work planning and managing the central database in a real project.  
- Relationship to experiment: Indirect beneficiary; relies on accurate, complete measurements synchronized from all devices. Microserices-Sync de-risks the backend behavior they will depend on.

**QA Engineer (Validation Support, Optional)**  
- Role & context: May be involved in validating that sync behavior matches expectations in an emulated environment.  
- Relationship to experiment: Uses Microserices-Sync scenarios (e.g., 5 clients × 10 measurements) to confirm that no data is lost and that all clients and the server converge to the same dataset.

### User Journey

**Discovery and Adoption (Developers/Architect)**  
- The XR/AR developer or solution architect anticipates a future project that will require multi-client HoloLens-to-server sync and recognizes that data-loss risk is high if left untested.  
- They decide to create or adopt Microserices-Sync as a focused experiment to answer "how do we sync safely?" before a real contract is in place.

**Onboarding and Setup**  
- The XR/AR developer (and later backend dev) clones the Microserices-Sync repository, starts the Docker environment, and reviews the documented sync scenarios and data model.  
- They configure multiple ClientService instances and the ServerService to mirror the intended real-world pattern (several devices, one central store).

**Core Usage**  
- They run the key experiment scenario: 5 ClientService instances each generate 10 measurements, push them to ServerService, then pull back the consolidated measurement set.  
- They repeat or vary runs as needed, inspecting logs and simple views to verify that all measurements are present and consistent across server and all clients.

**Success Moment**  
- They confirm that all ClientService instances and the ServerService end up with the same complete set of measurements, with no missing or duplicated records.  
- They understand which design patterns, data structures, and sync flows made this work, and feel confident about reusing them in a real project.

**Long-Term Use**  
- Microserices-Sync becomes an internal reference: a go-to example for future XR/AR engineers, backend devs, and architects when designing similar systems.  
- The company benefits by having a pre-validated sync approach ready when bidding on or starting real projects.

## Success Metrics

For Microserices-Sync, success is primarily about data integrity and repeatable sync behavior in realistic multi-client scenarios.

From a user (developer/architect) perspective, the experiment is successful when:

- After a configurable multi-client sync scenario (for example, 5 ClientService instances with 10 measurements each), the ServerService and every ClientService instance end up with:
  - Exactly the same number of measurements.
  - Exactly the same set of measurement records (no missing, duplicated, or mismatched entries).
- The number of measurements generated per client can be configured (and optionally randomized), and the system still converges correctly for all tested configurations.
- CRUD operations performed on Measurements at the ServerService (add/edit/delete) are correctly propagated to all ClientService instances on the next sync, with no stale or orphan data left behind.
- The core scenarios (initial multi-client push/pull, server-side CRUD then sync) can be repeated multiple times, from a clean seeded state, with zero data-loss incidents.

### Business Objectives

Although Microserices-Sync is an experiment project rather than a production product, it still has clear business-facing objectives:

- Provide a concrete, reusable reference implementation for multi-client HoloLens-to-server synchronization that can be adopted in at least one future real project.
- Reduce architectural and implementation risk for future tenders by demonstrating that the chosen sync approach works reliably in realistic scenarios (multiple clients, CRUD on server, configurable data volume).
- Enable the core project roles (XR/AR developer, backend developer, solution architect) to align on a proven sync pattern before a real engagement starts.

### Key Performance Indicators

To make these objectives measurable, the following KPIs can be used:

- **Data Integrity KPIs**
  - After each completed sync run, the total number of measurements on ServerService equals the expected count based on:
    - Sum of all generated client measurements.
    - Plus server-side additions.
    - Minus server-side deletions.
  - Discrepancy count between ServerService and each ClientService instance after sync:
    - Target: 0 record mismatches per run.
- **Scenario Reliability KPIs**
  - Number of consecutive successful executions of the "5 clients with 10 measurements" scenario with zero data loss incidents:
    - Target: ≥ 10 consecutive runs without discrepancies.
  - Number of different (clients, measurements-per-client) configurations tested (e.g., 3-5 variations) with consistent, lossless convergence across all services:
    - Target: All tested configurations complete with 0 mismatches.
- **Conflict Resolution KPIs**
  - For each designed conflict scenario (e.g., two clients updating measurements related to the same cell, or server-side edits vs client-side changes), the observed outcome matches the defined conflict resolution rule (such as last-write-wins or explicit server-side override) in 100% of test runs.
  - Number of unresolved or ambiguous conflicts discovered during testing:
    - Target: 0 unresolved conflicts in the finalized experiment suite.
- **Environment Control KPIs**
  - Availability of a "clean and seed" operation that:
    - Clears all runtime data and reseeds a predefined baseline dataset on ServerService (excluding Measurements).
    - Can be run before any scenario to ensure a known starting state.
  - Successful verification that running "clean and seed" followed by a full scenario always results in consistent, repeatable outcomes:
    - Target: 100% of test cycles that start from the seed state meet the data integrity KPIs above.

## MVP Scope

### Core Features

- Two microservices: ServerService and ClientService, each running in its own container with dedicated volumes for the database and for logs.  
- Shared SQL schema covering core entities (Buildings, Rooms, Surfaces, Cells, Measurements, Users) aligned with the experiment's data model.  
- Standard ASP.NET MVC + jqGrid endpoints for each table on both services where applicable: GetPaged, GetById, Create, Update, Delete.  
- A single homepage per service that hosts jqGrid-based tables for all relevant entities (no separate pages per table).  
- Sync endpoints and logic:
  - ClientService: endpoints/UI actions to trigger upload of local Measurements to ServerService and to pull consolidated Measurements back.
  - ServerService: endpoints to receive client changes and expose the consolidated dataset.  
- Ability to run multiple ClientService instances (e.g., via docker-compose) pointing at one ServerService to emulate multi-client scenarios.  
- Basic configuration for how many measurements each client generates (with support for randomness), so different sync scenarios can be exercised.  

### Out of Scope for MVP

- Authentication and authorization (no login/roles; all operations are open within the experiment environment).  
- Advanced reporting, analytics dashboards, or complex visualizations beyond the jqGrid-based tables.  
- Rich, polished UI/UX; only a simple homepage layout with grids for CRUD and sync verification.  
- Non-essential features unrelated to demonstrating sync correctness (e.g., notifications, exports, integrations with external systems).  
- Production-grade observability and monitoring stacks (full APM, tracing, etc.); only minimal logging necessary to verify sync behavior.  
- Large variety of conflict strategies; for MVP only a small, demonstrable set (e.g., a default strategy plus optionally one alternative) managed from ServerService via simple toggles.

### MVP Success Criteria

- The core multi-client scenario (for example, 5 ClientService instances with 10 measurements each) can be executed end-to-end, with ServerService and all ClientService instances converging to the same complete set of measurements, with no missing or duplicated records.  
- CRUD operations (add/edit/delete) performed on Measurements at ServerService are correctly reflected on all ClientService instances after the next sync cycle.  
- At least one conflict resolution strategy is implemented and verified with explicit conflict scenarios, and the observed results match the chosen rules.  
- All previously defined success metrics that apply to the MVP (data integrity checks, repeatable runs from a clean seeded state) are satisfied for the covered scenarios.

### Future Vision

- Expanding the set of conflict resolution strategies (e.g., last-write-wins, server-preferred, client-preferred, rule-based) and providing clearer configuration and diagnostics for each.  
- Richer UI for inspecting sync history, conflicts, and resolution decisions (timelines, diff views, filters).  
- Additional scenarios beyond measurements (e.g., partial offline updates to other entities if a real project requires it).  
- Hardening for production-like environments: authentication/authorization, more robust observability, performance and scale testing with larger datasets and more client instances.  
- Potential reuse of Microserices-Sync as an internal template or starter kit for future HoloLens + backend projects.

