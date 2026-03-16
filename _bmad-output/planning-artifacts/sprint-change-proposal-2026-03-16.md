# Sprint Change Proposal — Epic 5: Polishing

**Date:** 2026-03-16  
**Proposed by:** Eugen  
**Workflow:** correct-course  
**Change type:** New Epic Addition (additive — no existing stories modified)

---

## Section 1: Issue Summary

The core implementation (Epics 1–4) is functionally complete. Two targeted improvements have been identified that fix a known defect in the Reset to Baseline behavior and improve operational observability through Serilog file logging. These are collected under a new **Epic 5 – Polishing**, which is the designated vehicle for fixes, incremental updates, and workflow improvements discovered during or after the main sprint.

**Change 1 – Reset to Baseline Completeness (Story 5.1)**  
Story 1.5 implemented `DatabaseResetter.ResetServerAsync` and the UI reset buttons ("Reset to Baseline" on ServerService, "Reset Client DB" on ClientService). Later, Story 3.1 introduced the `SyncRun` entity and a `SyncRuns` table in `ServerDbContext`. The `DatabaseResetter.ResetServerAsync` method was never updated to include `SyncRuns` in its bulk-delete sequence, so clicking "Reset to Baseline" leaves stale sync run history in the database. No other missing tables were found — `ClientDbContext` does not expose `SyncRuns` and its delete sequence is complete.

**Change 2 – Serilog File Logging (Story 5.2)**  
Story 3.4 added structured console logging via `AddSimpleConsole`. Docker volumes for log files (`/app/logs`) are already provisioned in `docker-compose.yml` for every service container but are currently unused, as no file sink was configured. Serilog file logging makes logs inspectable after container restarts and enables persistent log retention aligned with the already-mounted volumes.

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|------|--------|
| Epic 1 – Environment, Seeding, and Reset Baseline | Not affected. Story 1.5 stays **done**. The fix is carried in Epic 5. |
| Epic 2 – Multi-Client Sync Scenarios | Not affected. |
| Epic 3 – Sync Diagnostics and Audit | Not affected. Story 3.4 stays **done**. Serilog extends the existing logging. |
| Epic 4 – Developer Documentation and Guidance | Not affected. No doc updates requested. |
| **Epic 5 – Polishing** | **New.** Two stories added. |

### Story Impact

- No existing stories are reopened. All Epics 1–4 stories remain in their current status.
- Story 5.1 resolves the incomplete reset defect without touching Story 1.5.
- Story 5.2 extends Story 3.4's logging work with a file sink without modifying Story 3.4.

### Artifact Conflicts

| Artifact | Action |
|----------|--------|
| PRD | No update required (per user request). |
| Architecture | No update required (per user request). |
| Epics document | Append new Epic 5 section with Stories 5.1 and 5.2. |
| Implementation artifacts | Create `5-1-update-reset-to-baseline.md` and `5-2-add-serilog-file-logging.md`. |

### Technical Impact

| File | Change |
|------|--------|
| `Sync.Infrastructure/Data/DatabaseResetter.cs` | Add one `ExecuteDeleteAsync` call for `SyncRuns`. |
| `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs` | Add SyncRun seed data and zero-count assertion to server reset tests. |
| `ServerService/ServerService.csproj` | Add `Serilog.AspNetCore` and `Serilog.Sinks.File` package references. |
| `ClientService/ClientService.csproj` | Add same Serilog package references. |
| `ServerService/Program.cs` | Replace `AddSimpleConsole` with Serilog bootstrap + `UseSerilog`. |
| `ClientService/Program.cs` | Same Serilog replacement. |
| `ServerService/appsettings.json` | Add `Serilog` configuration section (console + file sinks). |
| `ClientService/appsettings.json` | Add `Serilog` configuration section (console + file sinks). |

---

## Section 3: Recommended Approach

**Approach: Direct Adjustment** — Add two new stories to a new Epic 5. Implement directly.

- No architectural rework.
- No rollback of completed work.
- Both stories are small, targeted, and can be implemented independently in any order.

**Change scope classification:** **Minor** — direct implementation by the development team.

---

## Section 4: Detailed Change Proposals

### Story 5.1 – Update Reset to Baseline Completeness

**Root cause:** `SyncRun` (FK → `Users.Id`, nullable) was added to `ServerDbContext` by Story 3.1 after `DatabaseResetter.ResetServerAsync` was implemented by Story 1.5. The delete sequence was never updated.

```
DatabaseResetter.ResetServerAsync — OLD delete sequence:
  Measurements → Cells → Surfaces → Rooms → Buildings → Users

NEW delete sequence:
  Measurements → SyncRuns → Cells → Surfaces → Rooms → Buildings → Users
```

**Rationale:** `SyncRuns` has a nullable FK to `Users`. It must be deleted before `Users` to satisfy the FK constraint. Inserting it between `Measurements` and `Cells` maintains the existing FK-safe ordering for all other tables.

**Unit test change:**
```
In SeedServerWithMeasurementsAsync — add one SyncRun record after seeding.
In all three ResetServerAsync test methods — add:
  Assert.Equal(0, await _serverDb.SyncRuns.CountAsync())
```

**Note for future stories:** If a new table with an FK dependency is added to `ServerDbContext` or `ClientDbContext` in a future polishing story, `DatabaseResetter` must be updated in the same story's task list.

---

### Story 5.2 – Serilog File Logging

**Root cause:** `docker-compose.yml` provisions `/app/logs` volumes for all service containers (e.g., `serverservice_logs:/app/logs`, `clientservice_logs_user1:/app/logs`) but no file sink writes to them. Logs are only available via `docker logs` and are lost on container restart.

**NuGet packages to add to both `ServerService.csproj` and `ClientService.csproj`:**
```xml
<PackageReference Include="Serilog.AspNetCore" Version="*latest compatible with net10.0*" />
<PackageReference Include="Serilog.Sinks.File" Version="*latest compatible with net10.0*" />
```

**Program.cs pattern (both services):**
```csharp
// 1. Bootstrap logger (before builder) — captures startup errors before Serilog is configured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// 2. Replace AddSimpleConsole with UseSerilog — reads full config from appsettings.json
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// 3. Add Log.CloseAndFlush() at end of Program.cs to flush file sink on shutdown
```

**appsettings.json Serilog section (ServerService):**
```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "System": "Warning"
    }
  },
  "WriteTo": [
    { "Name": "Console", "Args": { "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}" } },
    { "Name": "File", "Args": { "path": "logs/serverservice-.log", "rollingInterval": "Day", "retainedFileCountLimit": 7, "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}" } }
  ],
  "Enrich": ["FromLogContext"]
}
```

*(ClientService: same structure, file path `logs/clientservice-.log`)*

**Rationale:** `path: "logs/..."` is relative to the working directory. In Docker, the working directory is `/app`, so logs write to `/app/logs/` which is the mounted volume path — no hardcoded absolute path needed.

---

## Section 5: Implementation Handoff

**Scope:** Minor  
**Route to:** Development team for direct implementation  
**Implementation artifact files to create:**
- `_bmad-output/implementation-artifacts/5-1-update-reset-to-baseline.md`
- `_bmad-output/implementation-artifacts/5-2-add-serilog-file-logging.md`

**Success criteria:**
- [ ] `ResetServerAsync` deletes `SyncRuns` before deleting `Users`.
- [ ] Unit tests assert `SyncRuns.Count == 0` after `ResetServerAsync`.
- [ ] All existing unit tests continue to pass.
- [ ] Both services write rolling daily log files to the Docker-mounted `/app/logs` volume.
- [ ] `docker-compose logs` still shows console output (Serilog Console sink preserved).
- [ ] No hardcoded absolute file paths for logs (relative `logs/` path used).
