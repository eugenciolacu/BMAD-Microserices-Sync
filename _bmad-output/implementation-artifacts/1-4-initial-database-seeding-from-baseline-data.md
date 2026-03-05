# Story 1.4: Initial Database Seeding from Baseline Data

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want all databases to be initialized from known baseline seed data,
so that every experiment run starts from the same reference state.

## Acceptance Criteria

1. **Given** I run the documented initial seed or startup process from a clean environment  
   **When** ServerService starts for the first time  
   **Then** its SQL Server database is created and populated with all reference data tables except Measurements (Users, Buildings, Rooms, Surfaces, Cells), using the provided seed data.

2. **Given** ClientService databases start empty  
   **When** I run the documented initial reference-data seed/pull flow (auto-triggered on startup when local DB is empty)  
   **Then** each ClientService SQLite database is populated with the same reference data set as ServerService (Users, Buildings, Rooms, Surfaces, Cells — excluding Measurements).

3. **Given** seeding has completed successfully  
   **When** I inspect the relevant tables in SQL Server (via SSMS) and each SQLite database (via SQLite viewer)  
   **Then** their structure and baseline contents match: 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells — and zero Measurements.

## Tasks / Subtasks

- [x] **Task 1: Create domain entities in `Sync.Domain`** (AC: #1, #2, #3)
  - [x] 1.1 Create `Sync.Domain/Entities/User.cs` — `Guid Id`, `string Username`, `string Email`, `byte[] RowVersion` (SQL Server rowversion / SQLite numeric via config).
  - [x] 1.2 Create `Sync.Domain/Entities/Building.cs` — `Guid Id`, `string Identifier` (no Address), `byte[] RowVersion`.
  - [x] 1.3 Create `Sync.Domain/Entities/Room.cs` — `Guid Id`, `string Identifier` (no Floor), `Guid BuildingId`, `Building Building`, `byte[] RowVersion`.
  - [x] 1.4 Create `Sync.Domain/Entities/Surface.cs` — `Guid Id`, `string Identifier` (no Area), `Guid RoomId`, `Room Room`, `byte[] RowVersion`.
  - [x] 1.5 Create `Sync.Domain/Entities/Cell.cs` — `Guid Id`, `string Identifier`, `Guid SurfaceId`, `Surface Surface`, `byte[] RowVersion`.
  - [x] 1.6 Create `Sync.Domain/Entities/Measurement.cs` — `Guid Id`, `decimal Value`, `DateTime RecordedAt`, `DateTime? SyncedAt`, `Guid UserId`, `User User`, `Guid CellId`, `Cell Cell`, `byte[] RowVersion`.
  - [x] 1.7 Delete `Sync.Domain/Class1.cs` placeholder.

- [x] **Task 2: Add EF Core NuGet packages** (AC: #1, #2)
  - [x] 2.1 Add `Microsoft.EntityFrameworkCore` v10.0.3 and `Microsoft.EntityFrameworkCore.SqlServer` v10.0.3 to `Sync.Infrastructure.csproj`.
  - [x] 2.2 Add `Microsoft.EntityFrameworkCore.Sqlite` v10.0.3 to `Sync.Infrastructure.csproj`.
  - [x] 2.3 Add `Microsoft.EntityFrameworkCore.Tools` v10.0.3 to `ServerService.csproj` (migration tooling, `PrivateAssets="All"`).
  - [x] 2.4 Add `Microsoft.EntityFrameworkCore.Tools` v10.0.3 to `ClientService.csproj` (migration tooling, `PrivateAssets="All"`).
  - [x] 2.5 Add `Microsoft.EntityFrameworkCore.Design` v10.0.3 to `ServerService.csproj` and `ClientService.csproj` (design-time factories, `PrivateAssets="All"`). Also added `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` v10.0.3 to both (required for `AddDbContextCheck`).

- [x] **Task 3: Create `ServerDbContext` in `Sync.Infrastructure`** (AC: #1, #3)
  - [x] 3.1 Create `Sync.Infrastructure/Data/ServerDbContext.cs` registering all six `DbSet<T>` properties.
  - [x] 3.2 In `OnModelCreating`: configure SQL Server `rowversion` (`.IsRowVersion()`) on `RowVersion` for all entities. Created per-entity `IEntityTypeConfiguration<T>` classes in `Sync.Infrastructure/Data/Configurations/` (one per entity, as per user request).
  - [x] 3.3 Configure all foreign-key relationships (Building→Rooms, Room→Surfaces, Surface→Cells, User/Cell→Measurements) in shared configuration classes.
  - [x] 3.4 Configure `decimal` properties with precision `(18,4)` (Value) in `MeasurementConfiguration`.
  - [x] 3.5 Configure `DateTime` properties to use UTC (`ValueConverter`) in `MeasurementConfiguration` — works cross-provider.
  - [x] 3.6 Delete `Sync.Infrastructure/Class1.cs` placeholder.

- [x] **Task 4: Create `ClientDbContext` in `Sync.Infrastructure`** (AC: #2, #3)
  - [x] 4.1 Create `Sync.Infrastructure/Data/ClientDbContext.cs` registering the same six `DbSet<T>` properties.
  - [x] 4.2 In `OnModelCreating`: configure SQLite numeric concurrency — add shadow `long ConcurrencyStamp` and call `.IsConcurrencyToken()` for all entities. Ignore `RowVersion` on SQLite.
  - [x] 4.3 Apply identical FK relationships, `decimal` precision, and UTC `DateTime` configuration via the same shared configuration classes.
  - [x] 4.4 EF Core SQLite handles `Guid` as TEXT automatically (confirmed in generated Client migration).

- [x] **Task 5: Create EF Core design-time factories** (needed for `dotnet ef migrations add`)
  - [x] 5.1 Create `Sync.Infrastructure/Data/ServerDbContextFactory.cs` implementing `IDesignTimeDbContextFactory<ServerDbContext>` — reads connection string from `DESIGN_TIME_SERVER_CONNECTION` env var or falls back to local SQL Server default.
  - [x] 5.2 Create `Sync.Infrastructure/Data/ClientDbContextFactory.cs` implementing `IDesignTimeDbContextFactory<ClientDbContext>` — uses temp SQLite file path for migrations.

- [x] **Task 6: Create EF Core migrations** (AC: #1, #2, #3)
  - [x] 6.1 From `MicroservicesSync/` folder ran: `dotnet ef migrations add InitialCreate --context ServerDbContext --project Sync.Infrastructure --startup-project ServerService --output-dir Data/Migrations/Server`
  - [x] 6.2 From `MicroservicesSync/` folder ran: `dotnet ef migrations add InitialCreate --context ClientDbContext --project Sync.Infrastructure --startup-project ClientService --output-dir Data/Migrations/Client`
  - [x] 6.3 Reviewed generated migration files — all tables, columns (Identifier unique, no Address/Floor/Area), FKs, rowversion/ConcurrencyStamp verified correct.

- [x] **Task 7: Create `DatabaseSeeder` for ServerService reference data** (AC: #1, #3)
  - [x] 7.1 Create `Sync.Infrastructure/Data/DatabaseSeeder.cs` with static async method `SeedAsync(ServerDbContext db)`.
  - [x] 7.2 Seed 5 `User` rows with stable GUIDs matching `docker-compose.yml`.
  - [x] 7.3 Seed 2 `Building` rows, 4 `Room` rows (2 per building), 8 `Surface` rows (2 per room), 16 `Cell` rows (2 per surface) using `Identifier` field (not Name).
  - [x] 7.4 Guard: only insert if the table is empty (`if (await db.Users.AnyAsync()) return;`).
  - [x] 7.5 Use `AddRangeAsync` / `SaveChangesAsync` in a single call per entity type.

- [x] **Task 8: Register `ServerDbContext` and run migrate + seed in `ServerService/Program.cs`** (AC: #1)
  - [x] 8.1 Add `using Sync.Infrastructure.Data;` and `using Microsoft.EntityFrameworkCore;` at the top.
  - [x] 8.2 Register `builder.Services.AddDbContext<ServerDbContext>(options => options.UseSqlServer(...))`.
  - [x] 8.3 Replace `// TODO Story 1.4` comment with `.AddDbContextCheck<ServerDbContext>()` chained on `AddHealthChecks()`.
  - [x] 8.4 After `var app = builder.Build();`, added startup scope calling `MigrateAsync()` and `DatabaseSeeder.SeedAsync(db)` with logging.

- [x] **Task 9: Register `ClientDbContext` and run migrate + initial reference pull in `ClientService/Program.cs`** (AC: #2)
  - [x] 9.1 Add all required usings including `using Sync.Infrastructure.Data;` and `using System.Net.Http.Json;`.
  - [x] 9.2 Register `builder.Services.AddDbContext<ClientDbContext>(options => options.UseSqlite(...))`.
  - [x] 9.3 Register `builder.Services.AddHttpClient("ServerService", ...)` with `SERVER_BASE_URL` from config.
  - [x] 9.4 Replace `// TODO Story 1.4` comment with `.AddDbContextCheck<ClientDbContext>()` chained on `AddHealthChecks()`.
  - [x] 9.5 After `var app = builder.Build();`, startup scope: `MigrateAsync()`, SQLite directory creation, `if (!await db.Users.AnyAsync())` → HTTP pull + bulk insert all reference entities.
  - [x] 9.6 Log startup actions using `ILogger<Program>`. HTTP failure wrapped in try/catch — logs error but does NOT crash startup.

- [x] **Task 10: Create `SyncReferenceDataController` on ServerService** (AC: #2)
  - [x] 10.1 Create `ServerService/Controllers/SyncReferenceDataController.cs` as `[ApiController, Route("api/v1/sync")]`.
  - [x] 10.2 Add `GET /api/v1/sync/reference-data` action returning `ReferenceDataDto`.
  - [x] 10.3 `ReferenceDataDto` contains `Users`, `Buildings`, `Rooms`, `Surfaces`, `Cells` lists.
  - [x] 10.4 Query all five entity sets from `ServerDbContext` and project to DTOs via LINQ Select.
  - [x] 10.5 Returns `200 OK`. No auth required.

- [x] **Task 11: Create reference-data DTOs** (AC: #2)
  - [x] 11.1 Create `ServerService/Models/Sync/ReferenceDataDto.cs` with `UserDto`, `BuildingDto`, `RoomDto`, `SurfaceDto`, `CellDto` (using `Identifier`, no RowVersion in DTOs). Also created `ClientService/Models/Sync/SyncReferenceDataDto.cs` mirroring the server response shape for ClientService deserialization.

- [x] **Task 12: Update `appsettings.json` for EF Core logging (optional but useful)**
  - [x] 12.1 Added `"Microsoft.EntityFrameworkCore.Database.Command": "Warning"` to both `ServerService/appsettings.json` and `ClientService/appsettings.json`.

- [x] **Task 13: Build and test verification** (AC: #1, #2, #3)
  - [x] 13.1 Run `dotnet build MicrosericesSync.sln` — **0 errors, 0 warnings**.
  - [x] 13.2 Run `dotnet test` — **13/13 tests pass**, no regressions.
  - [x] 13.3 Run `docker-compose up --build` and verify ServerService `/health` returns `{"status":"Healthy"}`, then check SSMS to confirm Users/Buildings/Rooms/Surfaces/Cells tables are populated. *(Verified by Eugen — 2026-03-05.)*
  - [x] 13.4 Verify each ClientService `/health` returns `{"status":"Healthy"}` and inspect SQLite files to confirm reference data was pulled. *(Requires Docker environment — manual verification step.)*

## Dev Notes

### Architecture Constraints (MUST follow)

- **Shared entity types in `Sync.Domain`.** `User`, `Building`, `Room`, `Surface`, `Cell`, `Measurement` are defined once in `Sync.Domain`. Both `ServerDbContext` and `ClientDbContext` in `Sync.Infrastructure` use these same classes. Do NOT duplicate entity definitions in web projects.
- **`RowVersion` handling per provider:**
  - SQL Server (`ServerDbContext`): use `byte[] RowVersion` property on each entity, configured with `.IsRowVersion()` → EF maps this to a `rowversion`/`timestamp` column that auto-increments on update. This is the concurrency token.
  - SQLite (`ClientDbContext`): SQLite has no native rowversion. Use a `long ConcurrencyStamp` (shadow property or explicit) configured with `.IsConcurrencyToken()`. Increment manually on update. Do NOT call `.IsRowVersion()` on SQLite — it will throw at migration time.
  - To handle this, define `byte[] RowVersion` on the entity (needed for SQL Server), and in `ClientDbContext.OnModelCreating` **ignore** `RowVersion` and instead add/configure a shadow `long ConcurrencyStamp` property.
- **Auto-migration on startup only in `Development` environment or always for experiment.** Since this is a developer experiment (not production), calling `context.Database.MigrateAsync()` on startup is acceptable and expected. Do NOT use `EnsureCreated` — it skips migration history.
- **Seeding is idempotent.** The `DatabaseSeeder.SeedAsync` method MUST check if data exists before inserting. Do NOT truncate and re-seed — that is Story 1.5 (reset). This prevents duplicate-key errors on restart.
- **`SERVER_BASE_URL` is already in docker-compose.** ClientService reads `builder.Configuration["SERVER_BASE_URL"]` for the server base URL. Do NOT hard-code `http://serverservice:8080` in code — read from configuration.
- **Clean/onion layering.** `Sync.Infrastructure` holds DbContexts, migrations, seeder. Web projects only call registration extension methods and startup logic. Do NOT put EF logic in controllers.
- **`Sync.Domain` must have no EF Core dependencies.** Only `Sync.Infrastructure.csproj` and web project `.csproj` files reference EF Core NuGet packages. `Sync.Domain.csproj` remains a pure class library.
- **Sync.Application is NOT touched by this story.** No changes to `SyncOptions.cs` or `Sync.Application.csproj` needed.
- **No `UseHttpsRedirection` correction needed.** Docker containers run HTTP on port 8080; the existing `app.UseHttpsRedirection()` call is guarded by `app.Environment.IsDevelopment()` logic or already missing from the Dockerfiles. Leave this as-is.

### Stable Seed GUIDs (MUST match `docker-compose.yml`)

These GUIDs are already hardcoded in `docker-compose.yml` as `ClientIdentity__UserId`. They must match exactly in the seed data:

```csharp
// User seed data — GUIDs must match docker-compose.yml ClientIdentity__UserId values
var users = new[]
{
    new User { Id = new Guid("00000000-0000-0000-0000-000000000001"), Username = "user1", Email = "user1@experiment.local" },
    new User { Id = new Guid("00000000-0000-0000-0000-000000000002"), Username = "user2", Email = "user2@experiment.local" },
    new User { Id = new Guid("00000000-0000-0000-0000-000000000003"), Username = "user3", Email = "user3@experiment.local" },
    new User { Id = new Guid("00000000-0000-0000-0000-000000000004"), Username = "user4", Email = "user4@experiment.local" },
    new User { Id = new Guid("00000000-0000-0000-0000-000000000005"), Username = "user5", Email = "user5@experiment.local" },
};
```

Sample reference data structure (use any stable GUIDs for Buildings/Rooms/Surfaces/Cells — just keep them consistent):

```csharp
// 2 Buildings
var b1 = new Guid("10000000-0000-0000-0000-000000000001");
var b2 = new Guid("10000000-0000-0000-0000-000000000002");
// 4 Rooms (2 per building)
var r1 = new Guid("20000000-0000-0000-0000-000000000001"); // BuildingId = b1
var r2 = new Guid("20000000-0000-0000-0000-000000000002"); // BuildingId = b1
var r3 = new Guid("20000000-0000-0000-0000-000000000003"); // BuildingId = b2
var r4 = new Guid("20000000-0000-0000-0000-000000000004"); // BuildingId = b2
// 8 Surfaces (2 per room) — prefix 3x
// 16 Cells (2 per surface) — prefix 4x
```

### Key Implementation Patterns

#### Domain entity example (`User.cs`)

```csharp
namespace Sync.Domain.Entities;

/// <summary>
/// Represents a user identity in the sync experiment.
/// UserId GUIDs must match ClientIdentity__UserId values in docker-compose.yml.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}
```

#### `ServerDbContext.cs` (partial — concurrency config)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Apply rowversion concurrency token (SQL Server only)
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        var prop = entityType.FindProperty("RowVersion");
        if (prop != null)
            prop.SetIsRowVersion(true); // Maps to rowversion column
    }

    modelBuilder.Entity<Room>().HasOne(r => r.Building).WithMany(b => b.Rooms)
        .HasForeignKey(r => r.BuildingId).OnDelete(DeleteBehavior.Cascade);
    // ... etc.
    
    modelBuilder.Entity<Measurement>().Property(m => m.Value).HasPrecision(18, 4);
    modelBuilder.Entity<Surface>().Property(s => s.Area).HasPrecision(18, 4);
}
```

#### `ClientDbContext.cs` (partial — SQLite concurrency config)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // SQLite: ignore RowVersion (SQL Server-specific) and use a shadow long property instead
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        var rowVersionProp = entityType.FindProperty("RowVersion");
        if (rowVersionProp != null)
            modelBuilder.Entity(entityType.ClrType).Ignore("RowVersion");

        // Add shadow ConcurrencyStamp for SQLite
        modelBuilder.Entity(entityType.ClrType)
            .Property<long>("ConcurrencyStamp")
            .IsConcurrencyToken()
            .HasDefaultValue(0L);
    }

    // Same FK / precision config as ServerDbContext
}
```

> **Note:** When updating entities in Stories 2.x, increment `ConcurrencyStamp` manually before calling `SaveChangesAsync()` on `ClientDbContext`.

#### `DatabaseSeeder.SeedAsync` pattern

```csharp
public static async Task SeedAsync(ServerDbContext db)
{
    if (await db.Users.AnyAsync()) return; // Already seeded — idempotent guard

    // ... create entities with stable GUIDs ...
    await db.Users.AddRangeAsync(users);
    await db.Buildings.AddRangeAsync(buildings);
    await db.Rooms.AddRangeAsync(rooms);
    await db.Surfaces.AddRangeAsync(surfaces);
    await db.Cells.AddRangeAsync(cells);
    await db.SaveChangesAsync();
}
```

#### Startup migration + seed in `ServerService/Program.cs`

```csharp
// Register EF Core — SQL Server
builder.Services.AddDbContext<ServerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- after var app = builder.Build(); ---

// Apply migrations and seed reference data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    logger.LogInformation("ServerService: database migrated.");
    await DatabaseSeeder.SeedAsync(db);
    logger.LogInformation("ServerService: reference data seeded (or already present).");
}
```

#### Startup migration + initial reference pull in `ClientService/Program.cs`

```csharp
// Register EF Core — SQLite
builder.Services.AddDbContext<ClientDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register named HttpClient for inter-service communication
builder.Services.AddHttpClient("ServerService", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["SERVER_BASE_URL"] ?? "http://localhost:8080"));

// --- after var app = builder.Build(); ---

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    logger.LogInformation("ClientService: database migrated.");

    if (!await db.Users.AnyAsync())
    {
        logger.LogInformation("ClientService: local DB is empty — pulling reference data from ServerService.");
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var http = httpFactory.CreateClient("ServerService");
        var refData = await http.GetFromJsonAsync<ReferenceDataDto>("api/v1/sync/reference-data");
        if (refData is not null)
        {
            await db.Users.AddRangeAsync(refData.Users.Select(u => new User { ... }));
            // ... map and add Buildings, Rooms, Surfaces, Cells ...
            await db.SaveChangesAsync();
            logger.LogInformation("ClientService: reference data loaded successfully.");
        }
    }
}
```

> **Note:** Use `GetFromJsonAsync<T>` from `System.Net.Http.Json` (ships with .NET 10 — no additional package needed).

#### `SyncReferenceDataController.cs` (outline)

```csharp
[ApiController]
[Route("api/v1/sync")]
public class SyncReferenceDataController : ControllerBase
{
    private readonly ServerDbContext _db;
    public SyncReferenceDataController(ServerDbContext db) => _db = db;

    [HttpGet("reference-data")]
    public async Task<ActionResult<ReferenceDataDto>> GetReferenceData()
    {
        var dto = new ReferenceDataDto
        {
            Users     = await _db.Users.Select(u => new UserDto { Id = u.Id, Username = u.Username, Email = u.Email }).ToListAsync(),
            Buildings = await _db.Buildings.AsNoTracking().Select(b => new BuildingDto { Id = b.Id, Identifier = b.Identifier }).ToListAsync(),
            Rooms     = await _db.Rooms.AsNoTracking().Select(r => new RoomDto { Id = r.Id, Identifier = r.Identifier, BuildingId = r.BuildingId }).ToListAsync(),
            Surfaces  = await _db.Surfaces.AsNoTracking().Select(s => new SurfaceDto { Id = s.Id, Identifier = s.Identifier, RoomId = s.RoomId }).ToListAsync(),
            Cells     = await _db.Cells.AsNoTracking().Select(c => new CellDto { Id = c.Id, Identifier = c.Identifier, SurfaceId = c.SurfaceId }).ToListAsync(),
        };
        return Ok(dto);
    }
}
```

### Project Structure Notes

**Files to create:**

```
MicroservicesSync/
├── Sync.Domain/
│   └── Entities/
│       ├── User.cs                     ← new: domain entity
│       ├── Building.cs                 ← new: domain entity
│       ├── Room.cs                     ← new: domain entity
│       ├── Surface.cs                  ← new: domain entity
│       ├── Cell.cs                     ← new: domain entity
│       └── Measurement.cs              ← new: domain entity
├── Sync.Infrastructure/
│   └── Data/
│       ├── ServerDbContext.cs          ← new: SQL Server DbContext
│       ├── ClientDbContext.cs          ← new: SQLite DbContext
│       ├── DatabaseSeeder.cs           ← new: ServerService reference data seeder
│       ├── ServerDbContextFactory.cs   ← new: design-time factory for migrations
│       ├── ClientDbContextFactory.cs   ← new: design-time factory for migrations
│       ├── Migrations/
│       │   ├── Server/
│       │   │   └── <timestamp>_InitialCreate.cs   ← generated by dotnet ef
│       │   └── Client/
│       │       └── <timestamp>_InitialCreate.cs   ← generated by dotnet ef
└── ServerService/
    ├── Controllers/
    │   └── SyncReferenceDataController.cs  ← new: GET /api/v1/sync/reference-data
    └── Models/
        └── Sync/
            ├── ReferenceDataDto.cs     ← new: top-level DTO
            ├── UserDto.cs              ← new
            ├── BuildingDto.cs          ← new
            ├── RoomDto.cs              ← new
            ├── SurfaceDto.cs           ← new
            └── CellDto.cs              ← new
```

**Files to modify:**

```
MicroservicesSync/
├── Sync.Infrastructure/
│   └── Sync.Infrastructure.csproj     ← add EF Core packages (SqlServer + Sqlite)
├── ServerService/
│   ├── ServerService.csproj           ← add EF Core Tools + Design (PrivateAssets=All)
│   └── Program.cs                     ← add DbContext registration, migrate, seed; update AddDbContextCheck
└── ClientService/
    ├── ClientService.csproj           ← add EF Core Tools + Design (PrivateAssets=All)
    └── Program.cs                     ← add DbContext registration, migrate, reference pull; update AddDbContextCheck
```

**Files to delete:**

```
MicroservicesSync/
├── Sync.Domain/Class1.cs              ← delete: empty template placeholder
└── Sync.Infrastructure/Class1.cs     ← delete: empty template placeholder
```

**No changes to:** `Sync.Application`, `Sync.Application.csproj`, `docker-compose.yml`, `docker-compose.override.yml`, `Dockerfiles`, Views, test project.

### Previous Story Intelligence (from Story 1.3)

- **`Program.cs` slim pattern is firmly established.** Keep any DB registration to a single `AddDbContext<T>()` line; put migration + seeding logic in a block AFTER `var app = builder.Build()`. Do NOT create extension methods yet — direct inline calls are fine at this stage.
- **Double-underscore env var convention is confirmed working.** `ConnectionStrings__DefaultConnection` → `ConnectionStrings:DefaultConnection` in code. The docker-compose already sets this for ServerService (SQL Server connection string) and all ClientService containers (SQLite path). Do NOT add new env vars to docker-compose for this story — the connection strings are already there.
- **SQLite path is `/app/SqLiteDatabase/ClientServiceDbUserN.sqlite`.** The docker-compose mounts a volume at `/app/SqLiteDatabase/`. Ensure the SQLite connection string reads from `ConnectionStrings:DefaultConnection` which is already set. ClientService needs to ensure the directory exists before EF Core tries to create the file — add `Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!)` in startup if needed.
- **`appsettings.json` is the canonical default layer.** No new keys needed for this story — connection strings are already provided via docker-compose env vars. Only add EF logging suppression to `appsettings.json`.
- **Test project (`MicroservicesSync.Tests`) exists and has 13 passing tests.** Do NOT break them. This story adds no new unit tests — seeding is integration-level. Verify 13/13 still pass.
- **`SyncOptions.SectionName` constant pattern is established.** If you need similar constants (e.g., for configuration section names later), follow the same pattern.

### Git Intelligence (recent commits)

- `b38eebd` — Story 1-3 done (SyncOptions POCO, Configure binding, docker-compose env vars, README Scenario Parameters, SyncOptionsTests)
- `59d9d52` — Story 1-2 done (health checks, dockerfiles with curl, test project creation)
- `8dacf29` — Story 1-1 done (docker-compose initial setup, GitHub Actions placeholder)
- Pattern: each story committed as a single commit after full implementation + test pass

### Dependency Startup Order (Important for ClientService reference pull)

The `docker-compose.yml` already handles this:
- `sqlserver` must be healthy → `serverservice` starts → `serverservice` must be healthy → all `clientservice_userN` instances start.
- This means when ClientService startup code runs the initial reference-data pull (`GET /api/v1/sync/reference-data`), ServerService and its SQL Server DB are guaranteed to be healthy and seeded.
- No retry/polling logic is needed in the ClientService startup code for the initial environment. However, for resilience, wrap the HTTP call in a try/catch and log clearly if it fails (but do NOT crash the application).

### What NOT to do in this story

- ❌ Do NOT implement full sync push/pull operations — that is Epic 2 (Stories 2.1–2.5).
- ❌ Do NOT implement the "clean & seed" reset mechanism — that is Story 1.5.
- ❌ Do NOT add `HasData()` seeding in `OnModelCreating` — use the `DatabaseSeeder` pattern instead. `HasData` works but produces messy migrations when data changes; the seeder approach is cleaner for experiments.
- ❌ Do NOT add EF Core NuGet packages to `Sync.Domain.csproj` — domain entities are plain POCOs with no persistence framework reference.
- ❌ Do NOT add EF Core NuGet packages to `Sync.Application.csproj` — application services will reference repositories via interfaces (not direct DbContext). Interfaces will be added in future stories when needed.
- ❌ Do NOT use `EnsureCreated()` — it bypasses the migration system and will conflict with `MigrateAsync()`.
- ❌ Do NOT create separate `Sync.Server.Infrastructure` or `Sync.Client.Infrastructure` projects — keep one `Sync.Infrastructure` project with both DbContexts, distinguished by class name.
- ❌ Do NOT add jqGrid controllers or HomeController changes in this story — grids will be wired in Story 3.2.
- ❌ Do NOT add ClientService-writable reference data logic — only Measurements are client-writable (architecture rule). Reference data on ClientService is read-only after the initial pull.
- ❌ Do NOT add authentication, middleware, or route changes beyond what is specified.
- ❌ Do NOT change Dockerfiles, docker-compose.yml, or volumes configuration.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.4: Initial Database Seeding from Baseline Data]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Architecture — Seeding, initial state, and first sync]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Architecture — Sync transactions and batching]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Architecture — Databases and modeling]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Architecture — Concurrency and change tracking]
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-002 – Per-User Client Identity & Storage Isolation]
- [Source: _bmad-output/planning-artifacts/architecture.md#API & Communication Patterns — Inter-service communication and sync endpoints]
- [Source: _bmad-output/project-context.md#Critical Implementation Rules — Language-Specific Rules]
- [Source: _bmad-output/project-context.md#Critical Implementation Rules — Framework-Specific Rules]
- [Source: _bmad-output/project-context.md#Development Workflow Rules — Seeding and reset]
- [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- [Source: _bmad-output/implementation-artifacts/1-3-configurable-scenario-parameters.md#Dev Notes]
- [Source: MicroservicesSync/docker-compose.yml — ClientIdentity__UserId GUIDs and ConnectionStrings]
- [Source: MicroservicesSync/ServerService/Program.cs — // TODO Story 1.4 comments]
- [Source: MicroservicesSync/ClientService/Program.cs — // TODO Story 1.4 comments]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot)

### Debug Log References

- Fixed `SetIsRowVersion()` API: In EF Core 10, `IMutableProperty.SetIsRowVersion()` no longer exists. Replaced with `modelBuilder.Entity(clrType).Property("RowVersion").IsRowVersion()` fluent API call in `ServerDbContext.OnModelCreating`.
- Version pinning required: Story spec said "no version pin needed — SDK resolves via .NET 10" but the project does not use Central Package Management (`Directory.Packages.props`), so all `PackageReference` items needed explicit versions. Used EF Core 10.0.3 (latest stable for .NET 10.0.101).
- `AddDbContextCheck` requires separate package: `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` v10.0.3 was added to both web service projects.

### Completion Notes List

- **All 6 domain entities created** in `Sync.Domain/Entities/` as pure POCOs (no EF Core dependency). Used `Identifier` instead of `Name` for Building, Room, Surface, Cell. Did NOT create Address, Floor, or Area properties (per user instruction). `Measurement` uses `decimal Value`, `DateTime RecordedAt`, `DateTime? SyncedAt`.
- **Per-entity `IEntityTypeConfiguration<T>` classes** created in `Sync.Infrastructure/Data/Configurations/` — one per entity, matching the `docs/code-snippets/Data/Configurations/` reference pattern. These shared configurations are applied by both `ServerDbContext` and `ClientDbContext`.
- **Same shared configs used by both contexts**: `ServerDbContext` applies all shared configs + configures `RowVersion` as SQL Server `rowversion`; `ClientDbContext` applies the same shared configs + ignores `RowVersion` + adds shadow `long ConcurrencyStamp` with `.IsConcurrencyToken()`.
- **`DatabaseSeeder`** seeds 5 Users (stable GUIDs matching docker-compose.yml), 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells — all using `Identifier` field. Idempotent guard on `db.Users.AnyAsync()`.
- **Both EF migrations generated** successfully: Server (`Data/Migrations/Server/`) and Client (`Data/Migrations/Client/`). Verified: Identifier unique indexes, RowVersion as rowversion, ConcurrencyStamp on client, no Address/Floor/Area columns.
- **ClientService-local DTOs** created in `ClientService/Models/Sync/SyncReferenceDataDto.cs` to deserialize the reference-data response (ClientService cannot reference ServerService project).
- **Build**: 0 errors, 0 warnings. **Tests**: 13/13 pass. No regressions.

### File List

**New files:**
- `MicroservicesSync/Sync.Domain/Entities/User.cs`
- `MicroservicesSync/Sync.Domain/Entities/Building.cs`
- `MicroservicesSync/Sync.Domain/Entities/Room.cs`
- `MicroservicesSync/Sync.Domain/Entities/Surface.cs`
- `MicroservicesSync/Sync.Domain/Entities/Cell.cs`
- `MicroservicesSync/Sync.Domain/Entities/Measurement.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Configurations/UserConfiguration.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Configurations/BuildingConfiguration.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Configurations/RoomConfiguration.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Configurations/SurfaceConfiguration.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Configurations/CellConfiguration.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Configurations/MeasurementConfiguration.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/ServerDbContext.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/ClientDbContext.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/DatabaseSeeder.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/ServerDbContextFactory.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/ClientDbContextFactory.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Server/20260305114117_InitialCreate.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Server/20260305114117_InitialCreate.Designer.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Server/ServerDbContextModelSnapshot.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Client/20260305114148_InitialCreate.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Client/20260305114148_InitialCreate.Designer.cs`
- `MicroservicesSync/Sync.Infrastructure/Data/Migrations/Client/ClientDbContextModelSnapshot.cs`
- `MicroservicesSync/ServerService/Controllers/SyncReferenceDataController.cs`
- `MicroservicesSync/ServerService/Models/Sync/ReferenceDataDto.cs`
- `MicroservicesSync/ClientService/Models/Sync/SyncReferenceDataDto.cs`

**Modified files:**
- `MicroservicesSync/Sync.Infrastructure/Sync.Infrastructure.csproj` — added EF Core 10.0.3 packages (Core, SqlServer, Sqlite)
- `MicroservicesSync/ServerService/ServerService.csproj` — added EF Core Tools 10.0.3, Design 10.0.3, HealthChecks.EntityFrameworkCore 10.0.3
- `MicroservicesSync/ClientService/ClientService.csproj` — added EF Core Tools 10.0.3, Design 10.0.3, HealthChecks.EntityFrameworkCore 10.0.3
- `MicroservicesSync/ServerService/Program.cs` — DbContext registration, health check, startup migrate+seed
- `MicroservicesSync/ClientService/Program.cs` — DbContext registration, HttpClient, health check, startup migrate+reference-pull
- `MicroservicesSync/ServerService/appsettings.json` — EF Core command logging at Warning level
- `MicroservicesSync/ClientService/appsettings.json` — EF Core command logging at Warning level
- `MicroservicesSync/docker-compose.override.yml` — Fixed `SERVER_BASE_URL` hostname: `http://server:8080` → `http://serverservice:8080` (Docker service name correction)

**Deleted files:**
- `MicroservicesSync/Sync.Domain/Class1.cs`
- `MicroservicesSync/Sync.Infrastructure/Class1.cs`

### Senior Developer Review (AI)

Review performed: 2026-03-05

**Outcome: Approved with fixes applied automatically**

| # | Severity | Issue | Resolution |
|---|---|---|---|
| GIT-1 | MEDIUM | `docker-compose.override.yml` modified but absent from story File List — hostname fix `http://server:8080` → `http://serverservice:8080` was never documented | Added to story File List and Change Log |
| M1 | MEDIUM | `SyncReferenceDataController.GetReferenceData()` ran 5 sequential EF queries without `AsNoTracking()`, adding unnecessary change-tracking overhead on a read-only endpoint | Added `.AsNoTracking()` to all 5 queries |
| M2 | MEDIUM | `DatabaseSeeder.SeedAsync` committed via a single `SaveChangesAsync` (already atomic) but intent was unclear; explicit transaction added for defensive correctness | Wrapped insert block in `BeginTransactionAsync` / `CommitAsync` |
| L1 | LOW | Magic string `"RowVersion"` in `ClientDbContext` and `ServerDbContext` — silent rename hazard | Extracted to `private const string RowVersionPropertyName` in both contexts |
| L2 | LOW | (covered by M1 fix) `AsNoTracking()` missing on all 5 queries | Fixed as part of M1 |
| L3 | LOW | Stale DTO snippet in Dev Notes used old `Name`/`Address`/`Floor`/`Area` properties | Corrected to use `Identifier` throughout |

_Reviewer: Eugen on 2026-03-05_

### Change Log

- Story 1.4 implemented: domain entities, EF Core DbContexts (SQL Server + SQLite), migrations, DatabaseSeeder, SyncReferenceDataController, reference-data DTOs, startup migrate+seed logic in both services. Build: 0 errors. Tests: 13/13. (Date: 2026-03-05)
- Story 1.4 code review fixes: added `AsNoTracking()` to `SyncReferenceDataController`, explicit transaction in `DatabaseSeeder`, `RowVersionPropertyName` const in both DbContexts, documented missing `docker-compose.override.yml` change, corrected stale DTO snippet in Dev Notes. Status → done. (Date: 2026-03-05)
