# Story 2.1: Client-Side Measurement Generation per Instance

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want each ClientService instance to generate its own configurable set of measurements,
so that I can emulate independent client activity before sync.

## Acceptance Criteria

1. **Given** a clean baseline state and configured measurement-per-client settings  
   **When** I trigger the documented measurement generation flow on each ClientService  
   **Then** each instance creates the expected number of new measurements tagged with its own client/user identity (from `ClientIdentity:UserId`).

2. **Given** measurements have been generated  
   **When** I inspect each ClientService database and Measurements grid  
   **Then** I see only that client's locally generated measurements, with correct metadata: `UserId` = client's configured identity, `RecordedAt` = UTC timestamp at generation time, `SyncedAt` = null, unique `Guid` IDs.

3. **Given** I adjust the configured `SyncOptions:MeasurementsPerClient` value  
   **When** I clear measurements (or reset from clean baseline) and rerun the generation flow  
   **Then** each client generates the new configured volume.

## Tasks / Subtasks

- [ ] **Task 1: Create `ClientIdentityOptions` POCO** (AC: #1, #2)
  - [ ] 1.1 Create `ClientService/Options/ClientIdentityOptions.cs`:
    ```csharp
    namespace ClientService.Options;

    public class ClientIdentityOptions
    {
        public const string SectionName = "ClientIdentity";
        public Guid UserId { get; set; }
    }
    ```
  - [ ] 1.2 Register in `ClientService/Program.cs` after the existing `SyncOptions` registration line:
    ```csharp
    builder.Services.Configure<ClientIdentityOptions>(
        builder.Configuration.GetSection(ClientIdentityOptions.SectionName));
    ```

- [ ] **Task 2: Create `MeasurementGenerationService`** (AC: #1, #2, #3)
  - [ ] 2.1 Create `ClientService/Services/MeasurementGenerationService.cs`:
    - `public class MeasurementGenerationService`
    - Constructor injects: `ClientDbContext db`, `IOptions<SyncOptions> syncOptions`,
      `IOptions<ClientIdentityOptions> clientIdentity`, `ILogger<MeasurementGenerationService> logger`
    - Private fields: `_db`, `_syncOptions` (unwrapped `.Value`), `_userId` (unwrapped `Guid`), `_logger`
  - [ ] 2.2 Implement `public async Task<int> GenerateMeasurementsAsync(CancellationToken cancellationToken = default)`:
    - Load cell IDs from local DB: `var cellIds = await _db.Cells.AsNoTracking().Select(c => c.Id).ToListAsync(cancellationToken);`
    - Guard — if `cellIds.Count == 0`: throw `InvalidOperationException("No cells found in local DB. Pull reference data from ServerService before generating measurements.")`
    - Generate `_syncOptions.MeasurementsPerClient` measurements — modulo over `cellIds` for distribution:
      ```csharp
      var now = DateTime.UtcNow;
      var count = _syncOptions.MeasurementsPerClient;
      var measurements = Enumerable.Range(0, count)
          .Select(i => new Measurement
          {
              Id = Guid.NewGuid(),
              Value = Math.Round((decimal)(Random.Shared.NextDouble() * 100), 2),
              RecordedAt = now,
              SyncedAt = null,
              UserId = _userId,
              CellId = cellIds[i % cellIds.Count]
          })
          .ToList();
      ```
    - `await _db.Measurements.AddRangeAsync(measurements, cancellationToken);`
    - `await _db.SaveChangesAsync(cancellationToken);`
    - Log generated count and UserId
    - Return `count`
  - [ ] 2.3 Add `// NOTE (M2.1):` comment at the top of the class explaining the direct `ClientDbContext`
    injection is an acknowledged deviation (consistent with `AdminController` `// NOTE (M4):` pattern).
    Rationale: full `IMeasurementRepository` abstraction is out of scope for Story 2.1.
  - [ ] 2.4 Register in `ClientService/Program.cs` (alongside AdminController's implicit DI, after DB registration):
    ```csharp
    builder.Services.AddScoped<MeasurementGenerationService>();
    ```

- [ ] **Task 3: Create `MeasurementsController` with generate endpoint** (AC: #1, #3)
  - [ ] 3.1 Create `ClientService/Controllers/MeasurementsController.cs`:
    - `[ApiController]`, `[Route("api/v1/measurements")]`, extends `ControllerBase`
    - Constructor injects: `MeasurementGenerationService generationService`, `ILogger<MeasurementsController> logger`
  - [ ] 3.2 Add `[HttpPost("generate")]` action:
    ```csharp
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _generationService.GenerateMeasurementsAsync(cancellationToken);
            _logger.LogInformation("ClientService: generated {Count} measurements.", count);
            return Ok(new { message = $"Generated {count} measurements.", count });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ClientService: measurement generation failed — prerequisite not met.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: measurement generation failed.");
            return StatusCode(500, new { message = "Generation failed.", error = ex.Message });
        }
    }
    ```
  - [ ] 3.3 **STOP HERE** — do NOT add `GetPaged`, `GetById`, `Create`, `Update`, or `Delete` endpoints.
    Those jqGrid CRUD methods are Story 3.2 scope.

- [ ] **Task 4: Add "Generate Measurements" UI to ClientService home page** (AC: #1, #2)
  - [ ] 4.1 Open `ClientService/Views/Home/Index.cshtml` — study the existing reset/pull-reference-data
    buttons added in Story 1.5 to match the visual and script style exactly.
  - [ ] 4.2 Add a "Sync Scenario Controls" (or similar) section with a **"Generate Measurements"** button:
    ```html
    <button onclick="generateMeasurements()" class="btn btn-primary">Generate Measurements</button>
    <span id="generateResult"></span>
    ```
  - [ ] 4.3 Add JavaScript function (consistent with existing fetch patterns in the view):
    ```javascript
    async function generateMeasurements() {
        const resp = await fetch('/api/v1/measurements/generate', { method: 'POST' });
        const data = await resp.json();
        document.getElementById('generateResult').textContent =
            resp.ok ? data.message : ('Error: ' + data.message);
    }
    ```
  - [ ] 4.4 Add a visible note near the button:
    _"Prerequisites: reset to baseline + pull reference data must be completed on this instance before generating."_

- [ ] **Task 5: Write tests** (AC: #1, #2, #3)
  - [ ] 5.1 Create `MicroservicesSync.Tests/Measurements/MeasurementGenerationTests.cs`
    - Copy the `IDisposable` + `SqliteConnection` + `ClientDbContext` setup pattern from
      `MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs` exactly.
    - Use `NullLogger<MeasurementGenerationService>.Instance` for the logger parameter.
    - Create options via: `Options.Create(new SyncOptions { MeasurementsPerClient = 10 })`
      and `Options.Create(new ClientIdentityOptions { UserId = new Guid("00000000-0000-0000-0000-000000000001") })`
    - Seed 5 cells into the in-memory ClientDbContext before each test that needs them.
  - [ ] 5.2 Implement the following test cases:
    - `GenerateMeasurements_DefaultConfig_CreatesCorrectCount`: 10 measurements inserted
    - `GenerateMeasurements_AllHaveCorrectUserId`: all rows have `UserId == configuredUserId`
    - `GenerateMeasurements_AllHaveSyncedAtNull`: all rows have `SyncedAt == null`
    - `GenerateMeasurements_AllHaveRecordedAtUtc`: all `RecordedAt` within a 5-second window of `DateTime.UtcNow`
    - `GenerateMeasurements_ValueInExpectedRange`: all `Value` between 0m and 100m inclusive
    - `GenerateMeasurements_NoCells_ThrowsInvalidOperation`: zero cells → `InvalidOperationException` thrown
    - `GenerateMeasurements_CustomCount_RespectsConfig`: `MeasurementsPerClient = 3` → exactly 3 rows
  - [ ] 5.3 Run `dotnet build MicrosericesSync.sln` — 0 errors, 0 warnings.
  - [ ] 5.4 Run `dotnet test` — all 19 existing tests pass; new generation tests pass (26 total: 19 + 7).
  - [ ] 5.5 Manual Docker smoke test: `docker-compose up --build`, then on each client container call
    `POST http://localhost:500X/api/v1/measurements/generate`, inspect SQLite DB to confirm count,
    `UserId`, and `SyncedAt = null`.

## Dev Notes

### Architecture Constraints (MUST follow)

- **`MeasurementGenerationService` lives in `ClientService/Services/`**, NOT in `Sync.Application`.
  This is deliberate for Story 2.1: the service requires `ClientDbContext` (an Infrastructure type),
  and building the full `IMeasurementRepository` / `IRepository<T>` abstraction layer is story 2.2+ scope.
  Document with `// NOTE (M2.1):` exactly as `AdminController.cs` uses `// NOTE (M4):`.

- **`ClientIdentityOptions` lives in `ClientService/Options/`**, not in `Sync.Application`.
  It is ClientService-specific configuration (each container is bound to a different UserId) and
  must not leak into shared Application or Infrastructure libraries.

- **`MeasurementsController` scope is ONLY the `generate` endpoint in this story.**
  Do NOT add jqGrid CRUD endpoints (`GetPaged`, `GetById`, `Create`, `Update`, `Delete`) — those
  belong to Story 3.2 (Measurement Inspection via jqGrid on Both Services).

- **No server communication in this story.** Generation is entirely local to the ClientService container's
  SQLite database. No HTTP calls to `ServerService`. The `IHttpClientFactory` is NOT needed here.

- **Use `DateTime.UtcNow`** for `RecordedAt`. Never `DateTime.Now`.

- **`SyncedAt` must be `null` after generation.** This field is set to a non-null UTC timestamp
  only after a successful push to ServerService (Story 2.2). Leaving it null is a design contract,
  not an omission.

- **`ConcurrencyStamp` (SQLite shadow property)** is managed automatically by `ClientDbContext`.
  `OnModelCreating` configures `HasDefaultValue(0L)` for the shadow `long ConcurrencyStamp` property
  on every entity. EF Core uses this default when inserting new rows — do NOT set it manually.
  Concurrency conflict handling during update is a Story 2.2+ concern.

- **`RowVersion` on the `Measurement` entity is ignored by `ClientDbContext`.**
  `ClientDbContext.OnModelCreating` explicitly calls `Ignore("RowVersion")` for each entity type
  that has it. Leave `Measurement.RowVersion` at its property-initializer default `Array.Empty<byte>()`.
  Do NOT attempt to configure or set it in ClientService.

- **Use `Random.Shared.NextDouble()`** — this static, thread-safe `Random` instance was introduced
  in .NET 6. Do NOT use `new Random()` per call or per instance.

- **Parameterized queries only.** Use EF Core LINQ (`AddRangeAsync`, `ToListAsync`). No raw SQL.

- **Do NOT generate measurements on container startup.** Generation is developer-triggered only.

### Key Implementation Files

| File | Action | Notes |
|---|---|---|
| `ClientService/Options/ClientIdentityOptions.cs` | **CREATE** | `SectionName = "ClientIdentity"`, `UserId` Guid property |
| `ClientService/Services/MeasurementGenerationService.cs` | **CREATE** | Business logic, acknowledged direct-DbContext deviation |
| `ClientService/Controllers/MeasurementsController.cs` | **CREATE** | `POST /api/v1/measurements/generate` only |
| `ClientService/Program.cs` | **MODIFY** | +2 `Configure<>` lines, +1 `AddScoped<>` line |
| `ClientService/Views/Home/Index.cshtml` | **MODIFY** | Add Generate button + fetch script |
| `MicroservicesSync.Tests/Measurements/MeasurementGenerationTests.cs` | **CREATE** | 7 test cases |

### Reference Files (read before writing)

- [ClientService/Controllers/AdminController.cs](../../../MicroservicesSync/ClientService/Controllers/AdminController.cs)
  — Established pattern: direct `ClientDbContext` injection in a ClientService class with `// NOTE` comment.
  Match this style for `MeasurementGenerationService`.
- [ClientService/Program.cs](../../../MicroservicesSync/ClientService/Program.cs)
  — DI registration style. The `IOptions<SyncOptions>` registration is the exact pattern to follow
  for `ClientIdentityOptions`. Add registrations alongside existing ones — do NOT restructure the file.
- [Sync.Application/Options/SyncOptions.cs](../../../MicroservicesSync/Sync.Application/Options/SyncOptions.cs)
  — POCO style to follow for `ClientIdentityOptions`.
- [MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs](../../../MicroservicesSync/MicroservicesSync.Tests/Reset/DatabaseResetterTests.cs)
  — Copy the `IDisposable` + `SqliteConnection` + `ClientDbContext` setup constructor and `Dispose`
  pattern verbatim into `MeasurementGenerationTests.cs`.
- [Sync.Infrastructure/Data/ClientDbContext.cs](../../../MicroservicesSync/Sync.Infrastructure/Data/ClientDbContext.cs)
  — Understand shadow `ConcurrencyStamp` and ignored `RowVersion` before touching Measurements.
- [ClientService/Views/Home/Index.cshtml](../../../MicroservicesSync/ClientService/Views/Home/Index.cshtml)
  — Study the existing reset / pull-reference-data buttons from Story 1.5. Match the HTML/JS style.

### Key Data Facts (from Seed — [DatabaseSeeder.cs](../../../MicroservicesSync/Sync.Infrastructure/Data/DatabaseSeeder.cs))

Post-baseline state on each ClientService (after reference data pull from ServerService):

| Table | Count | Notes |
|---|---|---|
| Users | 5 | GUIDs `00000000-0000-0000-0000-00000000000[1–5]` |
| Buildings | 2 | `building-alpha`, `building-beta` |
| Rooms | 4 | 2 per building |
| Surfaces | 8 | 2 per room |
| Cells | **16** | GUIDs `40000000-0000-0000-0000-00000000000[01–16]` — generation distributes across these |
| Measurements | 0 | → created by this story |

Each ClientService container's `ClientIdentity__UserId` maps to exactly one seeded User GUID:

| Container | Port | `ClientIdentity__UserId` |
|---|---|---|
| clientservice_user1 | 5001 | `00000000-0000-0000-0000-000000000001` |
| clientservice_user2 | 5002 | `00000000-0000-0000-0000-000000000002` |
| clientservice_user3 | 5003 | `00000000-0000-0000-0000-000000000003` |
| clientservice_user4 | 5004 | `00000000-0000-0000-0000-000000000004` |
| clientservice_user5 | 5005 | `00000000-0000-0000-0000-000000000005` |

### CellId Distribution Strategy

With 16 cells available and a default of 10 measurements per client, use modulo cycling:
`cellIds[i % cellIds.Count]` — this distributes measurements across cells without bias for small volumes.

- Do NOT hardcode any Cell GUIDs — always read `cellIds` from the local DB at runtime.
- Do NOT sort or shuffle; simple index modulo is intentional and predictable for experiment runs.

### `SyncOptions:MeasurementsPerClient` Configuration Cascade

The effective value (highest priority wins):
1. Environment variable `SyncOptions__MeasurementsPerClient` (set in docker-compose.yml per service, currently `10`)
2. `appsettings.Development.json` — `SyncOptions:MeasurementsPerClient`
3. `appsettings.json` — `SyncOptions:MeasurementsPerClient` (currently `10`)

IOptions is already registered in Program.cs. Do NOT read from `IConfiguration` directly for this value.

### Test Help: Creating `IOptions<T>` Without DI

```csharp
using Microsoft.Extensions.Options;

// Create options directly for test injection:
var syncOpts   = Options.Create(new SyncOptions { MeasurementsPerClient = 10 });
var identityOpts = Options.Create(new ClientIdentityOptions { UserId = new Guid("00000000-0000-0000-0000-000000000001") });
var logger     = NullLogger<MeasurementGenerationService>.Instance;
// NullLogger from: Microsoft.Extensions.Logging.Abstractions (available transitively via ClientService reference)
```

The test project already references `ClientService.csproj`, which brings in `Microsoft.Extensions.Options`
and `Microsoft.Extensions.Logging.Abstractions` transitively. No new NuGet package required.

### Seed helper for tests

```csharp
private async Task SeedCellsAsync(ClientDbContext db, int count = 5)
{
    var surfaceId = Guid.NewGuid();
    // Insert minimal Surface parent to satisfy FK
    db.Surfaces.Add(new Surface { Id = surfaceId, Identifier = "test-surface",
        RoomId = await EnsureRoomAsync(db) });
    for (int i = 0; i < count; i++)
        db.Cells.Add(new Cell { Id = Guid.NewGuid(), Identifier = $"test-cell-{i}", SurfaceId = surfaceId });
    await db.SaveChangesAsync();
}
```

For a minimal FK chain (Cell → Surface → Room → Building) use `Guid.NewGuid()` parents — the
in-memory SQLite DB does not enforce FK constraints by default, so you may skip parent rows
if constraint enforcement is off. Verify by running tests — if FK errors appear, add the parent rows.

### What NOT to do in This Story

- ❌ Do NOT implement push to ServerService — that is Story 2.2
- ❌ Do NOT implement pull from ServerService — that is Story 2.3
- ❌ Do NOT add jqGrid CRUD endpoints for Measurements (`GetPaged`, `GetById`, etc.) — that is Story 3.2
- ❌ Do NOT create a `MeasurementService` in `Sync.Application` — the full repository/Application
  service abstraction layer is Story 2.2 groundwork; adding it here is scope creep
- ❌ Do NOT auto-generate measurements on container startup
- ❌ Do NOT use `DateTime.Now` — always `DateTime.UtcNow`
- ❌ Do NOT use `new Random()` — use `Random.Shared.NextDouble()` (.NET 6+, thread-safe)
- ❌ Do NOT hardcode Cell or User GUIDs in business logic — always read from DB / IOptions
- ❌ Do NOT omit `CancellationToken` — pass it through every async call
- ❌ Do NOT add authentication or authorization
- ❌ Do NOT restructure `Program.cs` — append only, minimal changes

### Epic 1 Retrospective Learnings Applied

From [`epic-1-retro-2026-03-05.md`](epic-1-retro-2026-03-05.md):

1. **"What NOT to do" is a first-class quality tool** — enforced above. The list above actively
   prevents five realistic scope creeps (push, pull, jqGrid, Application layer, startup auto-generation).

2. **`SyncOptions` POCO and `IOptions<SyncOptions>` are ready for Epic 2 injection** (retro success #6).
   Use `IOptions<SyncOptions>` for `MeasurementsPerClient` — do NOT read `IConfiguration` directly.

3. **`Program.cs` thin-registration pattern** — add exactly two `Configure<>` lines and one `AddScoped<>`
   line. Do NOT add orchestration or business logic to `Program.cs`.

4. **Partial extractions are debt** — the TODO for `ReferenceDataLoader` extraction (duplicate between
   `Program.cs` and `AdminController.cs`) is a known open item. Do NOT create another partial
   duplicate. If you inline similar logic, leave a `// TODO` with a ticket reference.

5. **The `ConcurrencyStamp` (SQLite) vs `RowVersion` (SQL Server) seam is the deepest technical seam.**
   For this story: new Measurement inserts use `ConcurrencyStamp` default `0L` — no action needed.
   Story 2.2 will document the full translation strategy for sync update conflict handling.

6. **Manual Docker smoke test is a quality gate.** After implementation, run
   `docker-compose up --build`, hit `POST http://localhost:5001/api/v1/measurements/generate`,
   and verify via SQLite viewer that 10 rows exist with correct `UserId` and `SyncedAt = null`.

### Project Structure Reference

```
MicroservicesSync/
  ClientService/
    Controllers/
      AdminController.cs        ← reference for acknowledged-deviation pattern
      HomeController.cs         ← reference for view-serving controller
      MeasurementsController.cs ← CREATE (generate endpoint only)
    Options/
      ClientIdentityOptions.cs  ← CREATE
    Services/
      MeasurementGenerationService.cs ← CREATE
    Views/
      Home/
        Index.cshtml            ← MODIFY: add Generate button
    Program.cs                  ← MODIFY: +2 Configure<>, +1 AddScoped<>
  Sync.Application/
    Options/
      SyncOptions.cs            ← READ ONLY (MeasurementsPerClient, BatchSize)
  Sync.Infrastructure/
    Data/
      ClientDbContext.cs        ← READ ONLY (understand ConcurrencyStamp + RowVersion ignore)
      DatabaseSeeder.cs         ← READ ONLY (understand seeded cell GUIDs)
  MicroservicesSync.Tests/
    Measurements/
      MeasurementGenerationTests.cs ← CREATE (7 test cases)
    Reset/
      DatabaseResetterTests.cs  ← READ ONLY (copy setup pattern)
```

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
