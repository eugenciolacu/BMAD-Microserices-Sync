# Story 1.2: Service Health and Status Verification

Status: done

## Story

As a developer,
I want a simple way to verify that ClientService and ServerService are healthy,
so that I can confirm the environment is ready before running scenarios.

## Acceptance Criteria

1. **Given** all services are started  
   **When** I hit `GET /health` on ServerService (`http://localhost:5000/health`) or on any ClientService (`http://localhost:5001/health` through `http://localhost:5005/health`)  
   **Then** each running service returns HTTP 200 with a JSON body confirming `"status": "Healthy"`.

2. **Given** a service is intentionally stopped or misconfigured  
   **When** I call the same `/health` endpoint (or Docker queries the container healthcheck)  
   **Then** the stopped service returns no response (TCP refused / timeout), and a misconfigured ClientService missing its required `ClientIdentity__UserId` returns HTTP 503 with `"status": "Unhealthy"`.

3. **Given** the health checks are documented  
   **When** a new developer follows the README instructions  
   **Then** they can determine within minutes whether the environment is healthy by checking the listed `/health` URLs.

4. **Given** the healthchecks are wired into docker-compose  
   **When** I run `docker-compose ps`  
   **Then** `serverservice` and all five `clientservice_userN` containers show `(healthy)` status alongside their `running` state.

## Tasks / Subtasks

- [x] **Task 1: Register ASP.NET Core health checks in ServerService** (AC: #1, #4)
  - [x] 1.1 Add `builder.Services.AddHealthChecks()` in `ServerService/Program.cs`.
  - [x] 1.2 Add `app.MapHealthChecks("/health")` after `app.UseRouting()` and before `app.MapControllerRoute(...)` in `ServerService/Program.cs`.
  - [x] 1.3 Confirm the endpoint returns HTTP 200 with `{"status":"Healthy"}` when the service is running (manual or test).

- [x] **Task 2: Register ASP.NET Core health checks in ClientService with config validation** (AC: #1, #2, #4)
  - [x] 2.1 Add `builder.Services.AddHealthChecks()` in `ClientService/Program.cs`.
  - [x] 2.2 Add a custom inline health check that validates the `ClientIdentity:UserId` configuration key is set and is a non-empty GUID. Use `IConfiguration` injected into the lambda via `.AddCheck(...)`.
  - [x] 2.3 Add `app.MapHealthChecks("/health")` in `ClientService/Program.cs` (same placement as Task 1).
  - [x] 2.4 Verify that a running ClientService with `ClientIdentity__UserId` set returns HTTP 200 `{"status":"Healthy"}`.
  - [x] 2.5 Verify that if `ClientIdentity__UserId` is empty or missing, the endpoint returns HTTP 503 `{"status":"Unhealthy"}` (test locally by temporarily removing the env var).

- [x] **Task 3: Add docker-compose healthchecks for serverservice and all clientservice containers** (AC: #4)
  - [x] 3.1 Add `healthcheck` block to the `serverservice` service in `docker-compose.yml`.
  - [x] 3.2 Add the same `healthcheck` block to each of the five `clientservice_userN` services in `docker-compose.yml`.
  - [x] 3.3 Update each `clientservice_userN`'s `depends_on` block to wait for `serverservice` to be `service_healthy`.
  - [x] 3.4 Run `docker-compose up --build` and confirm `docker-compose ps` shows `(healthy)` for all six application containers.

- [x] **Task 4: Smoke-test all health endpoints** (AC: #1, #2, #3)
  - [x] 4.1 With all containers running, `curl http://localhost:5000/health` → HTTP 200 `{"status":"Healthy"}`.
  - [x] 4.2 Repeat for `http://localhost:5001/health` through `http://localhost:5005/health` → all HTTP 200 `{"status":"Healthy"}`.
  - [x] 4.3 Run `docker-compose stop clientservice_user1` and verify `curl http://localhost:5001/health` fails with "Connection refused" and `docker-compose ps` shows `(unhealthy)` for that container.
  - [x] 4.4 Restart via `docker-compose start clientservice_user1` and confirm it returns to `(healthy)`.

## Dev Notes

### Architecture Constraints (MUST follow)

- **Do NOT add any EF Core / database health checks in this story.** Database connections and DbContexts are introduced in Story 1.4. Adding them here would cause the health check to fail before the DB setup exists. A `-- TODO: add DbContext health check in story 1.4` comment is acceptable as a placeholder.
- **Health check endpoint MUST be `/health`** — this is the value referenced in docker-compose and will be documented in the README. Do not change to `/healthz`, `/status`, or any other path without updating compose and README at the same time.
- **Keep `Program.cs` thin.** Do not move health check registration to a separate extension class at this stage; a simple inline registration is sufficient and consistent with the Story 1.1 scaffold.
- **curl availability in containers.** The `mcr.microsoft.com/dotnet/aspnet:10.0` image is based on `debian:12-slim` and includes `curl`. The `CMD-SHELL` approach used by the `sqlserver` service in the existing `docker-compose.yml` is the correct pattern to follow.
- **Use ASP.NET Core 8+ built-in health checks** (`Microsoft.Extensions.Diagnostics.HealthChecks`). This package is already included in the ASP.NET Core framework — NO extra NuGet package is required for basic app-level checks.
- **Custom config validation check must use `IHealthCheck` or the `AddCheck` lambda** — do NOT put validation logic in `Program.cs` startup that throws; that would crash the container instead of reporting it as unhealthy.
- **Preserve clean/onion layering.** Health check registration belongs in the web project `Program.cs`. Do NOT add any health check logic to `Sync.Application` or `Sync.Domain`.

### Implementation Pattern — ServerService/Program.cs (after Task 1)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();
// TODO Story 1.4: add .AddDbContextCheck<ServerDbContext>() here

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
```

### Implementation Pattern — ClientService/Program.cs (after Task 2)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks()
    .AddCheck("client-identity-config", () =>
    {
        var userId = builder.Configuration["ClientIdentity:UserId"];
        return Guid.TryParse(userId, out var guid) && guid != Guid.Empty
            ? HealthCheckResult.Healthy("ClientIdentity:UserId is configured")
            : HealthCheckResult.Unhealthy("ClientIdentity:UserId is missing or invalid");
    });
// TODO Story 1.4: add .AddDbContextCheck<ClientDbContext>() here

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
```

> **Note:** `builder.Configuration` is accessible inside the `AddCheck` lambda because it is called during the builder phase before `app = builder.Build()`. This is safe and idiomatic for .NET 10.

### Implementation Pattern — docker-compose.yml (healthcheck block)

Add to `serverservice` and each `clientservice_userN`:

```yaml
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 20s
```

Change each `clientservice_userN` `depends_on` from:

```yaml
    depends_on:
      - serverservice
```

to:

```yaml
    depends_on:
      serverservice:
        condition: service_healthy
```

This mirrors the pattern already used by `serverservice` depending on `sqlserver`:
```yaml
    depends_on:
      sqlserver:
        condition: service_healthy
```

### Project Structure Notes

Only the following files are modified in this story:

```
MicroservicesSync/
├── docker-compose.yml                  ← add healthcheck blocks and service_healthy depends_on
├── ServerService/
│   └── Program.cs                      ← AddHealthChecks() + MapHealthChecks("/health")
└── ClientService/
    └── Program.cs                      ← AddHealthChecks() + custom config check + MapHealthChecks("/health")
```

**No new files are created.** No changes to Sync.Domain, Sync.Application, Sync.Infrastructure, Controllers, Views, or .csproj files are needed.

### Health Check URL Reference Table (for README in Story 4.1)

| Service | Internal URL | External (host) URL |
|---|---|---|
| ServerService | `http://serverservice:8080/health` | `http://localhost:5000/health` |
| ClientService User 1 | `http://clientservice_user1:8080/health` | `http://localhost:5001/health` |
| ClientService User 2 | `http://clientservice_user2:8080/health` | `http://localhost:5002/health` |
| ClientService User 3 | `http://clientservice_user3:8080/health` | `http://localhost:5003/health` |
| ClientService User 4 | `http://clientservice_user4:8080/health` | `http://localhost:5004/health` |
| ClientService User 5 | `http://clientservice_user5:8080/health` | `http://localhost:5005/health` |

### Previous Story Intelligence (from Story 1.1)

- **Established patterns:**
  - `Program.cs` follows slim builder pattern — keep additions minimal and inline.
  - Internal container port is `8080`; `ASPNETCORE_HTTP_PORTS=8080` is set in docker-compose.
  - docker-compose healthcheck style uses `CMD-SHELL` strings with `|| exit 1` (see `sqlserver` service).
  - `depends_on: condition: service_healthy` pattern is already established for `sqlserver` → `serverservice`.
  - `ClientIdentity__UserId` env var uses double-underscore `__` in docker-compose which maps to `ClientIdentity:UserId` in ASP.NET Core configuration. Access as `builder.Configuration["ClientIdentity:UserId"]`.
- **What NOT to carry forward:** Story 1.1 did NOT add EF Core, DB connections, or migrations. Do not slip any of that into this story.
- **File structure is clean scaffold** — both `Program.cs` files mirror each other at this stage. Divergence (the ClientService config check) is appropriately minimal.

### What NOT to do in this story

- ❌ Do NOT add EF Core, DbContexts, or database health check checks — that is Story 1.4.
- ❌ Do NOT change the `/health` path to `/healthz` or `/status`.
- ❌ Do NOT add `Microsoft.AspNetCore.Diagnostics.HealthChecks.UI` or any health-check UI package — this is an experiment tool, minimal is correct.
- ❌ Do NOT add `HealthCheckOptions` to customize the JSON response format unless AC testing reveals the default response format is insufficient.
- ❌ Do NOT add server-reachability checks from ClientService (pinging ServerService) — that is cross-service dependency checking and belongs in a later diagnostic story (Epic 3).
- ❌ Do NOT add authentication or authorization to the `/health` endpoint.
- ❌ Do NOT modify `HomeController`, views, or any entity/domain files.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.2: Service Health and Status Verification]
- [Source: _bmad-output/planning-artifacts/architecture.md#Infrastructure & Deployment]
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-002 – Per-User Client Identity & Storage Isolation]
- [Source: _bmad-output/project-context.md#Development Workflow Rules]
- [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- [Source: _bmad-output/implementation-artifacts/1-1-single-command-environment-startup.md#Dev Notes - Architecture Constraints]
- [Source: MicroservicesSync/docker-compose.yml — existing sqlserver healthcheck and depends_on pattern]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot)

### Debug Log References

### Completion Notes List

- Tasks 1–3 implemented and verified with `dotnet build` → 0 errors, 0 warnings.
- `ServerService/Program.cs`: Added `builder.Services.AddHealthChecks()` and `app.MapHealthChecks("/health")` before `app.MapControllerRoute(...)`. Added TODO placeholder for Story 1.4 DbContext check.
- `ClientService/Program.cs`: Added `builder.Services.AddHealthChecks().AddCheck("client-identity-config", ...)` with a custom lambda validating `ClientIdentity:UserId` is a non-empty GUID (Healthy/HTTP 200 when valid, Unhealthy/HTTP 503 when missing or invalid). Added `app.MapHealthChecks("/health")`. Added `using Microsoft.Extensions.Diagnostics.HealthChecks;` — no new NuGet package needed, ships with ASP.NET Core framework.
- `docker-compose.yml`: Added `healthcheck` block (`curl -sf http://localhost:8080/health || exit 1`, 15s interval, 5s timeout, 5 retries, 20s start_period) to `serverservice` and all five `clientservice_userN` services. Updated all five `clientservice_userN` `depends_on` entries from simple list form to `condition: service_healthy` map form, matching the existing `serverservice → sqlserver` pattern.
- curl not shipped in `aspnet:10.0` — fixed by adding `apt-get install -y curl` to the runtime stage of both Dockerfiles.
- Task 3.4 verified manually: `docker-compose ps` shows `(healthy)` for all 7 containers (sqlserver + serverservice + 5 clientservice_userN).
- Task 4 verified manually by Eugen (2026-03-04): all `/health` endpoints return HTTP 200 `{"status":"Healthy"}` under normal conditions; stopped container returns connection refused and shows `(unhealthy)`; restarted container returns to `(healthy)`.

### Code Review Fixes (2026-03-04 — Claude Sonnet 4.6)

**H1 Fixed** — `MapHealthChecks("/health")` now uses a custom `HealthCheckOptions.ResponseWriter` in both services that returns `Content-Type: application/json` with body `{"status":"Healthy"}` or `{"status":"Unhealthy"}`, satisfying AC1 and AC2 exactly.

**H2 Fixed** — Created `MicroservicesSync/README.md` with a health-check quick-reference table listing all six `/health` URLs and `docker-compose ps` instructions, satisfying AC3.

**M1 Fixed** — Added `sprint-status.yaml` and both Dockerfiles to File List (were changed but not documented).

**M2 Fixed** — Extracted health check decision logic into `ClientService/HealthChecks/ClientIdentityHealthCheck.cs` (internal static class). Updated `ClientService/Program.cs` to call it. Created `MicroservicesSync.Tests` xUnit project with 8 unit tests covering all valid/invalid/edge-case `UserId` inputs. All 8 tests pass. Added `InternalsVisibleTo` to `ClientService.csproj`. Added test project to `MicrosericesSync.sln`.

### File List

- `MicroservicesSync/ServerService/Program.cs`
- `MicroservicesSync/ClientService/Program.cs`
- `MicroservicesSync/ClientService/ClientService.csproj` ← added InternalsVisibleTo for test project
- `MicroservicesSync/ClientService/HealthChecks/ClientIdentityHealthCheck.cs` ← new: extracted testable health check logic
- `MicroservicesSync/docker-compose.yml`
- `MicroservicesSync/ServerService/Dockerfile` ← added curl install
- `MicroservicesSync/ClientService/Dockerfile` ← added curl install
- `MicroservicesSync/README.md` ← new: AC3 health check URL reference
- `MicroservicesSync/MicroservicesSync.Tests/MicroservicesSync.Tests.csproj` ← new: xUnit test project
- `MicroservicesSync/MicroservicesSync.Tests/HealthChecks/ClientIdentityHealthCheckTests.cs` ← new: 8 unit tests (all passing)
- `MicroservicesSync/MicrosericesSync.sln` ← added test project
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
