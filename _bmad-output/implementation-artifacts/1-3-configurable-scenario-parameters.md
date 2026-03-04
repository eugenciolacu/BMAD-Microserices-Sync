# Story 1.3: Configurable Scenario Parameters

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want to configure core scenario parameters (client count and measurement volume) without changing code,
so that I can vary experiment inputs easily and repeat scenarios with different settings.

## Acceptance Criteria

1. **Given** the environment is configured via documented config files or environment variables  
   **When** I change the number of `clientservice_userN` entries in `docker-compose.yml`  
   **Then** the next `docker-compose up` uses the new client count without any code recompilation.

2. **Given** measurement volume per client is configurable  
   **When** I update `SyncOptions__MeasurementsPerClient` in `docker-compose.yml` (or `appsettings.json`) for the relevant ClientService container(s)  
   **Then** sync scenarios use the new volume on the next run without requiring recompilation or code changes.

3. **Given** the sync batch size is configurable  
   **When** I update `SyncOptions__BatchSize` in `docker-compose.yml` or `appsettings.json`  
   **Then** push/pull operations in future stories will use the new batch size without code changes.

4. **Given** these parameters are documented in the README  
   **When** a new developer follows the instructions  
   **Then** they can adjust client count and measurement volume successfully on their first attempt.

## Tasks / Subtasks

- [x] **Task 1: Create `SyncOptions` POCO in Sync.Application** (AC: #2, #3)
  - [x] 1.1 Create `MicroservicesSync/Sync.Application/Options/SyncOptions.cs` with properties `BatchSize` (int, default 5) and `MeasurementsPerClient` (int, default 10). No NuGet dependencies needed — this is a plain C# class.
  - [x] 1.2 Delete the placeholder `Sync.Application/Class1.cs` (it is now superseded by the real content).

- [x] **Task 2: Bind `SyncOptions` in ServerService** (AC: #3)
  - [x] 2.1 Add `using Sync.Application.Options;` and `builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection("SyncOptions"));` to `ServerService/Program.cs` (after existing `AddHealthChecks()` line).
  - [x] 2.2 Add a `SyncOptions` section to `ServerService/appsettings.json` with default values `BatchSize: 5` and `MeasurementsPerClient: 10`.

- [x] **Task 3: Bind `SyncOptions` in ClientService** (AC: #2, #3)
  - [x] 3.1 Add `using Sync.Application.Options;` and `builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection("SyncOptions"));` to `ClientService/Program.cs` (after the existing `AddHealthChecks()` chain).
  - [x] 3.2 Add a `SyncOptions` section to `ClientService/appsettings.json` with default values `BatchSize: 5` and `MeasurementsPerClient: 10`.

- [x] **Task 4: Expose configuration via docker-compose environment variables** (AC: #1, #2, #3)
  - [x] 4.1 Add `SyncOptions__BatchSize=5` and `SyncOptions__MeasurementsPerClient=10` environment variables to the `serverservice` service in `docker-compose.yml`.
  - [x] 4.2 Add `SyncOptions__BatchSize=5` and `SyncOptions__MeasurementsPerClient=10` environment variables to all five `clientservice_userN` services in `docker-compose.yml`.
  - [x] 4.3 Add a comment block above the `clientservice_user1` service in `docker-compose.yml` explaining how to change client count (duplicate/remove a `clientservice_userN` block and a matching `User` seed entry; keep user GUIDs consistent with seed data from Story 1.4).

- [x] **Task 5: Update README with Scenario Parameters section** (AC: #4)
  - [x] 5.1 Add a "Scenario Parameters" section to `MicroservicesSync/README.md` with a table showing the two env-var-controlled parameters (`SyncOptions__MeasurementsPerClient`, `SyncOptions__BatchSize`) and a note explaining how to scale client count via docker-compose.
  - [x] 5.2 Verify the README section is comprehensible to a developer who has not read the architecture doc.

- [x] **Task 6: Build verification**
  - [x] 6.1 Run `dotnet build MicrosericesSync.sln` and confirm 0 errors, 0 warnings.
  - [x] 6.2 Run existing unit tests (`dotnet test`) and confirm all 8 existing health-check tests still pass.

## Dev Notes

### Architecture Constraints (MUST follow)

- **`SyncOptions` is a plain POCO.** Place it in `Sync.Application/Options/SyncOptions.cs`. Do NOT add EF Core, HTTP, or any framework dependency to `Sync.Application` as part of this story.
- **`Configure<SyncOptions>()` belongs in the web project `Program.cs`**, not in `Sync.Application` or `Sync.Infrastructure`. This follows the established pattern: infrastructure details (config binding) stay in the host/web layer.
- **Do NOT use `IOptions<T>` injection yet.** Just register the binding; actual injection into services happens when those services are created in Stories 2.x. This story establishes the configuration wiring, not the consumption.
- **Do NOT implement measurement generation logic.** Story 2.1 (Client-Side Measurement Generation) is the story that actually uses `MeasurementsPerClient`. This story only defines and wires the configuration.
- **Do NOT implement sync batch processing.** `BatchSize` is used in Stories 2.2 and 2.3. This story only establishes the option binding.
- **Client count is NOT a runtime configuration value in code.** The fixed 5×5×5 topology (ADR-002) means changing client count = editing docker-compose. This is intentional; document it clearly. Do NOT add a dynamic client count setting to the app code.
- **Keep `Program.cs` thin.** One new line per service for `Configure<SyncOptions>`. No extension methods needed at this stage.
- **`Class1.cs` placeholder must be removed.** It was the project template stub. Its deletion is a required part of Task 1.

### Implementation Patterns

#### `SyncOptions.cs` (new file)

```csharp
namespace Sync.Application.Options;

/// <summary>
/// Configurable parameters controlling Microserices-Sync experiment scenarios.
/// Bind via "SyncOptions" configuration section or SyncOptions__* environment variables.
/// </summary>
public class SyncOptions
{
    public const string SectionName = "SyncOptions";

    /// <summary>
    /// Number of measurements to generate per ClientService instance in a single scenario run.
    /// Override via environment variable: SyncOptions__MeasurementsPerClient
    /// Default: 10
    /// </summary>
    public int MeasurementsPerClient { get; set; } = 10;

    /// <summary>
    /// Number of measurement records per in-memory batch during a push or pull sync operation.
    /// The entire push/pull still executes inside a single DB transaction across all batches.
    /// Override via environment variable: SyncOptions__BatchSize
    /// Default: 5
    /// </summary>
    public int BatchSize { get; set; } = 5;
}
```

#### ServerService `Program.cs` addition (after `AddHealthChecks()`)

```csharp
// Sync scenario options — configurable via SyncOptions__* env vars or appsettings.json
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));
```

Add `using Sync.Application.Options;` at the top of the file (or use a global using in the project).

#### ClientService `Program.cs` addition (same location)

Identical to the ServerService addition above.

#### `appsettings.json` addition (both services)

```json
"SyncOptions": {
  "BatchSize": 5,
  "MeasurementsPerClient": 10
}
```

#### docker-compose.yml — env vars to add to each service

```yaml
    environment:
      # ... existing vars ...
      - SyncOptions__BatchSize=5
      - SyncOptions__MeasurementsPerClient=10
```

#### docker-compose.yml — client count comment block

Add above the `clientservice_user1` definition:

```yaml
  # ── To change client count: ────────────────────────────────────────────────
  # The MVP topology is fixed to 5 ClientService instances (ADR-002).
  # To add a 6th client:
  #   1. Add a clientservice_user6 block below (copy any existing block, bump ports and volumes).
  #   2. Set ClientIdentity__UserId to a new stable GUID matching a seeded user (Story 1.4).
  #   3. Add matching volumes at the bottom of this file.
  # To remove a client: delete the block and its volumes declaration.
  # Always keep user GUIDs consistent with the seed CSV (Story 1.4).
  # ──────────────────────────────────────────────────────────────────────────
```

### Project Structure Notes

Files to create:

```
MicroservicesSync/
└── Sync.Application/
    └── Options/
        └── SyncOptions.cs             ← new: POCO for scenario parameters
```

Files to modify:

```
MicroservicesSync/
├── docker-compose.yml                 ← add SyncOptions__ env vars + client count comment
├── ServerService/
│   ├── Program.cs                     ← add Configure<SyncOptions>() binding
│   └── appsettings.json               ← add SyncOptions section with defaults
└── ClientService/
    ├── Program.cs                     ← add Configure<SyncOptions>() binding
    └── appsettings.json               ← add SyncOptions section with defaults
```

Files to delete:

```
MicroservicesSync/
└── Sync.Application/
    └── Class1.cs                      ← delete: empty template placeholder
```

**No changes to:** `Sync.Domain`, `Sync.Infrastructure`, Controllers, Views, Dockerfiles, test project — this story is exclusively about configuration wiring.

### Previous Story Intelligence (from Story 1.2)

- **`Program.cs` slim pattern is firmly established.** Both `ServerService/Program.cs` and `ClientService/Program.cs` use inline, minimal registrations. Stay consistent: one `builder.Services.Configure<SyncOptions>(...)` line per service is sufficient — no extension class needed.
- **Double-underscore env var convention is confirmed working.** `ClientIdentity__UserId` → `ClientIdentity:UserId` in config. The same pattern applies here: `SyncOptions__BatchSize` → `SyncOptions:BatchSize` in code. The docker-compose already uses this convention.
- **`appsettings.json` is the canonical default layer.** Keep env vars in docker-compose aligned with the values in `appsettings.json` (both default to 5 and 10); developers only need to override the env vars when deviating. No separate `appsettings.Development.json` changes needed.
- **README.md already exists** (created in Story 1.2 for health-check URL reference). Add the Scenario Parameters section to it — do not create a new file.
- **Test project (`MicroservicesSync.Tests`) exists and has 8 passing tests.** Do NOT break them. This story adds no testable logic (no health check, no repository, no sync operation), so no new tests are required. Verify existing tests still pass (Task 6.2).
- **`curl` is available in containers** via apt-get install added in Story 1.2 Dockerfiles — no Dockerfile changes needed here.

### Git Intelligence (recent commits)

- `59d9d52` — Story 1-2 done (health checks, dockerfiles with curl, test project creation)
- `4dbe3f0` — Older ServerService code added as reference
- `8dacf29` — Story 1.1 implementation
- Pattern observed: each story is committed as a single commit after completion

### What NOT to do in this story

- ❌ Do NOT implement `MeasurementsPerClient` consumption — Story 2.1 consumes this option.
- ❌ Do NOT implement `BatchSize` consumption — Stories 2.2/2.3 use this.
- ❌ Do NOT inject `IOptions<SyncOptions>` anywhere yet — just register the binding.
- ❌ Do NOT add a runtime "client count" configuration key in app code — client count = docker-compose container count (ADR-002 is intentional).
- ❌ Do NOT add `Microsoft.Extensions.Options` NuGet package — it ships with ASP.NET Core and is already available in the web projects via the framework reference.
- ❌ Do NOT add `Microsoft.Extensions.Options` to `Sync.Application.csproj` — the POCO carries no framework dependency.
- ❌ Do NOT add `Microsoft.Extensions.Configuration` to `Sync.Application.csproj` — binding is the web project's responsibility.
- ❌ Do NOT modify Dockerfiles, Views, Controllers, or domain entities.
- ❌ Do NOT add a `[Range]` or validation attribute to `SyncOptions` yet — keep the POCO minimal.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.3: Configurable Scenario Parameters]
- [Source: _bmad-output/planning-artifacts/architecture.md#Sync transactions and batching]
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-002 – Per-User Client Identity & Storage Isolation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Configuration and environments]
- [Source: _bmad-output/project-context.md#Development Workflow Rules]
- [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- [Source: _bmad-output/project-context.md#Framework-Specific Rules]
- [Source: _bmad-output/implementation-artifacts/1-2-service-health-and-status-verification.md#Dev Notes]
- [Source: MicroservicesSync/docker-compose.yml — existing env var and healthcheck patterns]
- [Source: MicroservicesSync/ClientService/appsettings.json — existing config structure]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot)

### Debug Log References

- Fixed `using Sync.Application.Options;` placement in `ServerService/Program.cs` — must precede all statements in top-level program files (CS1529).
- Code review (2026-03-04): Fixed M1 (added `SyncOptionsTests.cs`, 5 unit tests), M2 (added explanatory comment to `ServerService/appsettings.json`), L1 (added XML doc to `SectionName`), L2 (fixed README port notation), L3 (fixed README table column header). All 13 tests pass.

### Completion Notes List

- Created `Sync.Application/Options/SyncOptions.cs` as a plain POCO with no framework dependencies. `SectionName` constant (with XML doc) keeps binding consistent across both services.
- Deleted `Sync.Application/Class1.cs` template placeholder.
- Added `using Sync.Application.Options;` + `Configure<SyncOptions>()` to both `ServerService/Program.cs` and `ClientService/Program.cs`. Using directives placed at file top (top-level statements requirement).
- Added `"SyncOptions"` section with `BatchSize: 5`, `MeasurementsPerClient: 10` defaults to both `appsettings.json` files. `ServerService/appsettings.json` includes inline comment explaining why `MeasurementsPerClient` lives in server config.
- Added `SyncOptions__BatchSize=5` and `SyncOptions__MeasurementsPerClient=10` env vars to `serverservice` and all five `clientservice_userN` services in `docker-compose.yml`.
- Added client count comment block above `clientservice_user1` in `docker-compose.yml`.
- Added "Scenario Parameters" section to `README.md` with parameter table (column header corrected to "Config path (ASP.NET notation)") and client count scaling instructions (port notation fixed).
- Added `SyncOptionsTests.cs` (5 unit tests) covering POCO defaults, `SectionName` constant, and mutability.
- Build: 0 errors, 0 warnings. Tests: 13/13 pass.

### File List

- `MicroservicesSync/Sync.Application/Options/SyncOptions.cs` — **created**
- `MicroservicesSync/Sync.Application/Class1.cs` — **deleted**
- `MicroservicesSync/ServerService/Program.cs` — modified: added `using` + `Configure<SyncOptions>()`
- `MicroservicesSync/ServerService/appsettings.json` — modified: added `SyncOptions` section with explanatory comment
- `MicroservicesSync/ClientService/Program.cs` — modified: added `using` + `Configure<SyncOptions>()`
- `MicroservicesSync/ClientService/appsettings.json` — modified: added `SyncOptions` section
- `MicroservicesSync/docker-compose.yml` — modified: added `SyncOptions__*` env vars to all 6 services + client count comment
- `MicroservicesSync/README.md` — modified: added "Scenario Parameters" section (column header + port notation fixed)
- `MicroservicesSync/MicroservicesSync.Tests/Options/SyncOptionsTests.cs` — **created** (5 unit tests)
- `MicroservicesSync/MicroservicesSync.Tests/MicroservicesSync.Tests.csproj` — modified: added `Sync.Application` project reference
