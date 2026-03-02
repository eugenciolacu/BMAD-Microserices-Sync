---
project_name: Microserices-Sync
user_name: Eugen
date: '2026-03-02'
title: Project Context for AI Agents
description: Critical rules and patterns that AI agents must follow when implementing Microserices-Sync.
communication_language: English
document_output_language: English
user_skill_level: intermediate
output_folder: c:/Eugen Files/Projects/BMAD-Microserices-Sync/_bmad-output
planning_artifacts: c:/Eugen Files/Projects/BMAD-Microserices-Sync/_bmad-output/planning-artifacts
implementation_artifacts: c:/Eugen Files/Projects/BMAD-Microserices-Sync/_bmad-output/implementation-artifacts
project_knowledge: c:/Eugen Files/Projects/BMAD-Microserices-Sync/docs
sections_completed:
  - technology_stack
  - language_rules
  - framework_rules
  - testing_rules
  - code_style_rules
  - workflow_rules
  - critical_rules
existing_patterns_found: 8
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. It focuses on unobvious details that are easy to miss during generation._

---

## How to Use This Document

- Read this before adding or changing HTTP endpoints, sync flows, or data models.
- Use it as the single reference when configuring docker-compose topology and seed/reset behavior.
- Keep this file updated whenever you introduce new architectural decisions or cross-cutting rules.

## Technology Stack & Versions

- Runtime & language: .NET 10, ASP.NET Core MVC (C#).
- Services:
  - ServerService – ASP.NET Core MVC using SQL Server via EF Core.
  - ClientService – ASP.NET Core MVC using SQLite via EF Core (one database per client instance).
  - Standard topology: exactly five ClientService instances, each representing a specific seeded user.
- Data access:
  - EF Core code-first with two DbContexts: `ServerDbContext` (SQL Server) and `ClientDbContext` (SQLite).
  - Shared domain entities live in Sync.Domain; mappings must keep table/column names aligned across providers.
- Orchestration & environment:
  - Docker + docker-compose as the primary way to run the system.
  - Separate containers per service; named volumes for databases and logs.
  - Expected developer environment: Windows + Visual Studio, local-only (no external cloud dependencies).
- UI:
  - Razor views with jqGrid-based tables for all main entities.
  - Single Home page per service hosting all grids and sync actions.

## Critical Implementation Rules

### Language-Specific Rules (C# / EF Core)

- Use `Guid` for all primary keys and foreign keys across both services so that IDs remain globally unique and portable between databases.
- Configure concurrency tokens per provider:
  - SQL Server: use a `rowversion`/`byte[]` column marked as a concurrency token.
  - SQLite: use a numeric/integer column configured as a concurrency token.
- Use UTC timestamps for all time-based fields that participate in sync or auditing.
- Use async/await for all I/O-bound operations (database and HTTP); avoid blocking calls like `.Result` or `.Wait()` in web code.
- Use EF Core with parameterized queries only:
  - Never concatenate SQL strings with user-supplied or jqGrid-supplied values.
  - Prefer LINQ expressions that EF Core translates into SQL.
- Keep entity classes in Sync.Domain free of persistence-specific concerns (no direct DB or HTTP calls from entities).

### Framework-Specific Rules (ASP.NET Core MVC + HTTP APIs)

- Maintain the solution structure with two MVC web projects plus shared libraries:
  - ServerService and ClientService projects reference Sync.Application and Sync.Infrastructure.
  - Sync.Application and Sync.Infrastructure reference Sync.Domain.
- Expose per-entity `[ApiController]` controllers with jqGrid-compatible endpoints:
  - `GetById(Guid id)`
  - `GetPaged(int page, int pageSize, string? sortBy, string? sortOrder, string? filters)`
  - `Create(...)`, `Update(Guid id, ...)`, `Delete(Guid id)`
- Capability rules per service:
  - ServerService: full CRUD for all entities (reference data + Measurements).
  - ClientService: full CRUD only for Measurements; all other entities are read-only (no create/update/delete endpoints or UI actions).
- Use versioned API routes consistently (for example `/api/v1/{entity}` and `/api/v1/sync/...`). Do not mix unversioned and versioned routes.
- Home controllers:
  - Each service exposes a single `HomeController.Index()` view that hosts all jqGrids for that service.
  - ClientService `HomeController` also exposes sync trigger actions (push/pull) that delegate to Application layer services.
- Keep controllers thin:
  - Move all business logic and sync orchestration into Sync.Application services.
  - Controllers should primarily handle HTTP concerns (model binding, validation, and mapping to DTOs/services).

### Testing Rules

- Prioritize integration-style tests around sync behavior:
  - Verify that ServerService and all ClientService instances converge to identical Measurement sets after push/pull flows.
  - Assert on final database state, not only HTTP response codes.
- When unit testing Application layer services, mock repositories and external HTTP clients but keep domain logic real.
- For HTTP-level tests, prefer using in-memory test hosts (e.g., ASP.NET Core test server) or thin end-to-end harnesses targeting the public API surface.
- Any change to sync logic (push/pull contracts, conflict resolution, seeding/reset) should be accompanied by updated tests that cover the modified flows.

### Code Quality & Style Rules

- Preserve clean/onion layering:
  - Web (MVC/API) → Application → Domain, with persistence-specific details isolated in Infrastructure.
  - Do not let Infrastructure reference Web projects.
- Place shared domain entities, value objects, and core enums in Sync.Domain; avoid duplicating these types in web projects.
- Keep Sync.Application focused on use-cases and orchestration logic:
	- No direct EF Core calls from controllers; use Application services instead.
	- Implement sync flows (push, pull, conflict resolution) in dedicated services rather than scattering logic.
- Maintain consistent naming:
  - Entities: singular (e.g., `Building`, `Room`, `Measurement`).
  - Tables and routes: consistent and predictable, matching entity names where possible.
  - Services: `*Service` for application services, `*Repository` for data access types.
- For jqGrid integration:
  - Whitelist sortable/filterable fields explicitly in controller or Application logic.
  - Map jqGrid paging/sorting/filtering parameters into strongly-typed queries; never pass raw column names or filter strings into SQL.
- Keep the project minimal and focused on sync correctness; avoid introducing unrelated frameworks or features that do not support the experiment's goals.

### Development Workflow Rules

- Docker + docker-compose is the canonical way to run the system:
  - Any scripts or changes must preserve `docker-compose` as the primary entrypoint for starting all services.
  - Containers must use volumes for database and log persistence.
- Seeding and reset:
  - ServerService database is seeded with reference data (all tables except Measurements).
  - Users and other reference data are seeded on ServerService from CSV/Excel files; GUIDs in seed files must remain stable once chosen.
  - ClientService databases start empty; the first sync operation pulls reference data from ServerService.
  - A reset operation must return the system to this baseline before scenarios are rerun.
- Documentation alignment:
	- Any change to schema, sync flows, or environment setup must be reflected in the README, PRD, and architecture documents.
	- Keep the experiment runnable and understandable by a new developer with only the README and this context file.
- Local-only assumption:
  - Do not introduce external cloud dependencies (managed SQL, cloud queues, etc.) without explicitly updating this context file and the architecture docs.

### Critical Don't-Miss Rules

- Do not add real authentication/authorization flows:
  - All actions are simulated, and data is synthetic; security beyond basic SQL injection protection is intentionally out of scope.
- Do not let schemas drift between ServerService and ClientService:
  - Entity shapes, key types, and table/column names must remain compatible so that sync mappings stay trivial.
- Do not allow ClientService to write reference data tables:
  - Only Measurements are client-writable; Buildings, Rooms, Surfaces, Cells, Users, and similar tables are read-only on clients.
- Do not bypass the sync pipeline:
  - All cross-service data movement must go through defined sync endpoints and Application services, not ad-hoc SQL or manual DB edits.
- Always enforce safe handling of jqGrid parameters:
  - Never inject `sortBy`, `sortOrder`, or `filters` directly into SQL.
  - Validate/whitelist, then translate into LINQ expressions.
- Maintain deterministic, repeatable runs:
  - Seed and reset flows must produce the same baseline every time so that sync scenarios are comparable across runs.
- Keep the experiment scoped:
  - If a change does not help prove sync correctness or improve diagnostics/maintainability, strongly question adding it.

- Preserve the fixed client/user topology:
  - Keep exactly five ClientService instances in the standard experiment setup, each bound to a single seeded user via the `ClientIdentity__UserId` environment variable and its own DB/log volumes.
  - If you change the number of clients or users, update seed files, docker-compose, and this context file together so identity and storage mappings stay consistent.

---

This file is the authoritative project context for all AI agents working on Microserices-Sync. Update it whenever new architectural decisions or implementation rules are introduced.