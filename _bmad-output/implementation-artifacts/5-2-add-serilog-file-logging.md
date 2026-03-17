# Story 5.2: Serilog File Logging

Status: done

## Story

As a developer,
I want both ServerService and ClientService to write structured logs to rolling daily files in the mounted `/app/logs` Docker volume,
so that I can inspect persistent log history after a container restart and correlate log entries with sync operations across multiple runs.

## Background

Story 3.4 configured structured console logging via `AddSimpleConsole` (with `IncludeScopes = true` and a timestamp format). `docker-compose.yml` already provisions named volumes for log files on every service container:
- `serverservice_logs:/app/logs` → ServerService
- `clientservice_logs_user1:/app/logs` → ClientService user1
- `clientservice_logs_user2:/app/logs` → ClientService user2 (and so on for all 5)

These volumes are mounted but unused — no file sink has been configured. This story replaces `AddSimpleConsole` with Serilog, which writes to both the console (preserving `docker logs` behavior) and to a rolling daily file in the mounted volume.

## Acceptance Criteria

1. **Given** the environment is running via docker-compose  
   **When** any log-worthy activity occurs on either service (startup, sync, reset, error)  
   **Then** log entries are written to a rolling daily file under `/app/logs/` in the respective service container.

2. **Given** I inspect a log file from a mounted Docker volume  
   **When** I open the file for ServerService or any ClientService instance  
   **Then** I see timestamped, levelled entries with source context, message, and exception details on failure — matching the information previously visible only in console output.

3. **Given** Serilog file logging is configured  
   **When** the service runs  
   **Then** `docker-compose logs` still shows full console output because the Serilog Console sink is active alongside the File sink.

4. **Given** the log file path in config uses a relative path (`logs/`)  
   **When** the app runs in Docker (working directory `/app`)  
   **Then** log files are written to `/app/logs/` which is the volume mount point — no hardcoded absolute paths.

## Tasks / Subtasks

- [x] **Task 1: Add Serilog NuGet packages to `ServerService/ServerService.csproj`** (AC: #1, #2, #3)
  - [x] 1.1 Add the following `PackageReference` entries to the existing `<ItemGroup>` containing package references:
    ```xml
    <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
    ```
    `Serilog.AspNetCore` transitively includes `Serilog.Sinks.Console` so no separate console-sink package is needed.

- [x] **Task 2: Add Serilog NuGet packages to `ClientService/ClientService.csproj`** (AC: #1, #2, #3)
  - [x] 2.1 Add the same two package references as Task 1 (`Serilog.AspNetCore 10.0.0` and `Serilog.Sinks.File 7.0.0`) to the existing `<ItemGroup>` containing packages in `ClientService.csproj`.

- [x] **Task 3: Configure Serilog in `ServerService/Program.cs`** (AC: #1, #2, #3, #4)
  - [x] 3.1 Add `using Serilog;` at the top of `ServerService/Program.cs`.
  - [x] 3.2 **Before** the `var builder = WebApplication.CreateBuilder(args);` line, add a bootstrap logger that captures any startup errors before the full Serilog configuration is loaded:
    ```csharp
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();
    ```
  - [x] 3.3 **Remove** the existing `builder.Logging.AddSimpleConsole(...)` block (all 5 lines from `builder.Logging.AddSimpleConsole(options =>` to the closing `});`).
  - [x] 3.4 **Add** the following immediately after `var builder = WebApplication.CreateBuilder(args);`:
    ```csharp
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());
    ```
    This reads the full Serilog configuration (sinks, levels, enrichers) from `appsettings.json`.
  - [x] 3.5 At the **very end** of `Program.cs`, after `app.Run();`, add:
    ```csharp
    Log.CloseAndFlush();
    ```
    This ensures buffered file sink entries are flushed on graceful shutdown.

- [x] **Task 4: Configure Serilog in `ClientService/Program.cs`** (AC: #1, #2, #3, #4)
  - [x] 4.1 Add `using Serilog;` at the top of `ClientService/Program.cs`.
  - [x] 4.2 Add bootstrap logger **before** `var builder = WebApplication.CreateBuilder(args);` (same as Task 3.2).
  - [x] 4.3 **Remove** the existing `builder.Logging.AddSimpleConsole(...)` block (same as Task 3.3).
  - [x] 4.4 **Add** `builder.Host.UseSerilog(...)` immediately after `var builder = WebApplication.CreateBuilder(args);` (same as Task 3.4).
  - [x] 4.5 Add `Log.CloseAndFlush();` at the very end of `Program.cs` after `app.Run();` (same as Task 3.5).

- [x] **Task 5: Add Serilog configuration section to `ServerService/appsettings.json`** (AC: #1, #2, #3, #4)
  - [x] 5.1 Add a `"Serilog"` top-level key to `ServerService/appsettings.json`. The existing `"Logging"` key can remain for any non-Serilog components that still read it, but Serilog will use its own section. Place the Serilog section after `"AllowedHosts"`:
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
        {
          "Name": "Console",
          "Args": {
            "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
          }
        },
        {
          "Name": "File",
          "Args": {
            "path": "logs/serverservice-.log",
            "rollingInterval": "Day",
            "retainedFileCountLimit": 7,
            "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
          }
        }
      ],
      "Enrich": ["FromLogContext"]
    }
    ```
    **Path note:** `"path": "logs/serverservice-.log"` is relative to the working directory. In Docker the working directory is `/app`, so the file is written to `/app/logs/serverservice-YYYYMMDD.log` — the volume-mounted path. Locally (running from Visual Studio), logs appear in the project's `bin/Debug/net10.0/logs/` folder. The `-` before the extension is the Serilog rolling-interval date suffix placeholder.

- [x] **Task 6: Add Serilog configuration section to `ClientService/appsettings.json`** (AC: #1, #2, #3, #4)
  - [x] 6.1 Add an identical `"Serilog"` section to `ClientService/appsettings.json`, with the file sink path changed to:
    ```json
    "path": "logs/clientservice-.log"
    ```
    All other settings (minimum levels, output templates, retainedFileCountLimit) are the same as Task 5.

## Dev Notes

- `Serilog.AspNetCore` replaces the default `ILogger<T>` provider entirely via `UseSerilog`. All existing `ILogger<T>` injection in controllers and services continues to work — no call sites change.
- `ReadFrom.Configuration` requires the `Serilog.Settings.Configuration` package, which is automatically included as a dependency of `Serilog.AspNetCore` — no separate package reference needed.
- The `ReadFrom.Services(services)` call allows Serilog to use any `ILogEventEnricher` or `ILogEventSink` registered in the DI container (not currently used, but is idiomatic Serilog setup).
- Keep the `Enrich.FromLogContext()` call in `UseSerilog` — this is what makes `ILogger.BeginScope(...)` structured context (used in Story 3.4) available in log output.
- The existing `"Logging"` section in `appsettings.json` is not removed. It serves as a fallback for any framework components that may still read from it (e.g., health checks). Having both sections is harmless.
- **Docker:** The rolling file name will be `serverservice-20260316.log` for a run on 2026-03-16. After 7 days, older files are automatically deleted (`retainedFileCountLimit: 7`).
- **No test changes required** for this story. The existing unit tests do not exercise `Program.cs` startup code or file I/O.

## Dev Agent Record

### Implementation Plan
Added `Serilog.AspNetCore 10.0.0` and `Serilog.Sinks.File 7.0.0` to both service `.csproj` files. Replaced `builder.Logging.AddSimpleConsole(...)` in both `Program.cs` files with a bootstrap logger and `builder.Host.UseSerilog(...)` reading full configuration from `appsettings.json`. Added `Log.CloseAndFlush()` after `app.Run()` in both files. Added `"Serilog"` configuration sections to both `appsettings.json` files with Console + rolling File sinks, relative `logs/` paths (resolves to `/app/logs/` in Docker), 7-day retention, and source-context output templates. The existing `"Logging"` section is preserved as a harmless fallback.

### Completion Notes
- ✅ Task 1: `Serilog.AspNetCore 10.0.0` and `Serilog.Sinks.File 7.0.0` added to `ServerService.csproj`.
- ✅ Task 2: Same packages added to `ClientService.csproj`.
- ✅ Task 3: `ServerService/Program.cs` — bootstrap logger added, `AddSimpleConsole` removed, `UseSerilog` added, `Log.CloseAndFlush()` added.
- ✅ Task 4: `ClientService/Program.cs` — same changes as Task 3.
- ✅ Task 5: `"Serilog"` section added to `ServerService/appsettings.json` with Console + File sinks (`logs/serverservice-.log`, rolling daily, 7-day retention).
- ✅ Task 6: `"Serilog"` section added to `ClientService/appsettings.json` with Console + File sinks (`logs/clientservice-.log`, rolling daily, 7-day retention).
- ✅ Build: 0 errors, 0 warnings.
- ✅ Tests: 64 passed. 5 pre-existing MeasurementPush/PullTests failures (NullReferenceException on `Request.Headers`) are unrelated to this story — see Story 5.1 notes.
- ✅ No test changes required per story Dev Notes.
- ✅ Code Review (M1): Both Program.cs files wrapped in try/catch/finally — Log.Fatal on unexpected host termination, Log.CloseAndFlush in finally.
- ✅ Code Review (M2): File List updated with AddSyncRunRowVersion migration files and updated snapshot.
- ✅ Code Review (L1): Bootstrap logger outputTemplate made consistent with main sink template.
- ✅ Code Review (L2): appsettings.Development.json updated for both services (Debug level, Console-only sink — file sink suppressed locally).

## File List

- `MicroservicesSync/ServerService/ServerService.csproj` — modified (added Serilog packages)
- `MicroservicesSync/ClientService/ClientService.csproj` — modified (added Serilog packages)
- `MicroservicesSync/ServerService/Program.cs` — modified (bootstrap logger with outputTemplate, try/catch/finally guard, UseSerilog, Log.CloseAndFlush)
- `MicroservicesSync/ClientService/Program.cs` — modified (bootstrap logger with outputTemplate, try/catch/finally guard, UseSerilog, Log.CloseAndFlush)
- `MicroservicesSync/ServerService/appsettings.json` — modified (added Serilog section)
- `MicroservicesSync/ClientService/appsettings.json` — modified (added Serilog section)
- `MicroservicesSync/ServerService/appsettings.Development.json` — modified (added Serilog Development override, Debug level, Console-only sink)
- `MicroservicesSync/ClientService/appsettings.Development.json` — modified (added Serilog Development override, Debug level, Console-only sink)
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Server/20260317084139_AddSyncRunRowVersion.cs` — added (migration for SyncRun.RowVersion column, created during Story 5.1 fix)
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Server/20260317084139_AddSyncRunRowVersion.Designer.cs` — added (migration designer)
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Server/ServerDbContextModelSnapshot.cs` — modified (updated snapshot to include SyncRun.RowVersion)

## Senior Developer Review (AI)

**Reviewer:** GitHub Copilot on 2026-03-17
**Verdict:** Changes Requested → Fixed

### Findings

| Severity | Finding | Location | Resolution |
|----------|---------|----------|------------|
| MEDIUM | No try/catch/finally Serilog host guard — `Log.CloseAndFlush()` placed after `app.Run()` only runs on graceful shutdown; exceptions during startup would lose buffered file-sink entries | `ServerService/Program.cs`, `ClientService/Program.cs` | Fixed: wrapped entire host setup in try/catch/finally with `Log.Fatal` on catch and `Log.CloseAndFlush` in finally |
| MEDIUM | Migration files missing from story File List — `AddSyncRunRowVersion.cs`, `AddSyncRunRowVersion.Designer.cs`, and updated `ServerDbContextModelSnapshot.cs` created during dev session not documented | File List | Fixed: all 3 migration files added to File List |
| LOW | Bootstrap logger `WriteTo.Console()` had no outputTemplate — pre-bootstrap messages used default Serilog format, inconsistent with configured sink template | `ServerService/Program.cs`, `ClientService/Program.cs` | Fixed: added matching outputTemplate to bootstrap `WriteTo.Console()` |
| LOW | `appsettings.Development.json` had no Serilog section — running locally in Development would write rolling file logs to `bin/Debug/net10.0/logs/`, unexpected for developers | `ServerService/appsettings.Development.json`, `ClientService/appsettings.Development.json` | Fixed: added Serilog override with Debug minimum level and Console-only sink (File sink suppressed in Development) |

**Post-fix:** Build 0 errors, 0 warnings. 64/69 tests pass (5 pre-existing unrelated failures).

## Change Log

- 2026-03-17: Added Serilog file logging to ServerService and ClientService — rolling daily files under `logs/` (resolves to `/app/logs/` in Docker), Console + File sinks, 7-day retention, `AddSimpleConsole` replaced by `UseSerilog` (Story 5.2).
- 2026-03-17: Code review fixes — try/catch/finally host guard (MEDIUM); bootstrap logger outputTemplate (LOW); appsettings.Development.json Serilog section with Console-only sink (LOW); File List updated with migration files (MEDIUM).
