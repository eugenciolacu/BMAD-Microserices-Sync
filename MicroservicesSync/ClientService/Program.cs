using ClientService.HealthChecks;
using ClientService.Models.Sync;
using ClientService.Options;
using ClientService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sync.Application.Options;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register EF Core — SQLite
builder.Services.AddDbContext<ClientDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Named HttpClient for inter-service communication with ServerService
builder.Services.AddHttpClient("ServerService", client =>
{
    var serverUrl = builder.Configuration["SERVER_BASE_URL"]        // docker/env
                 ?? builder.Configuration["ServerBaseUrl"]          // appsettings.json
                 ?? "http://localhost:8080";                        // ultimate fallback
    client.BaseAddress = new Uri(serverUrl);
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ClientDbContext>()
    .AddCheck("client-identity-config", () =>
        ClientIdentityHealthCheck.Evaluate(builder.Configuration["ClientIdentity:UserId"]));

// Sync scenario options — configurable via SyncOptions__* env vars or appsettings.json
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));
builder.Services.Configure<ClientIdentityOptions>(
    builder.Configuration.GetSection(ClientIdentityOptions.SectionName));

builder.Services.AddScoped<MeasurementGenerationService>();

var app = builder.Build();

// Apply migrations and pull reference data from ServerService if local DB is empty
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Ensure directory exists for the SQLite database file (volume-mounted in Docker)
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    var dataSourcePrefix = "Data Source=";
    if (connectionString.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
    {
        var sqlitePath = connectionString[dataSourcePrefix.Length..];
        var sqliteDir = Path.GetDirectoryName(sqlitePath);
        if (!string.IsNullOrEmpty(sqliteDir))
            Directory.CreateDirectory(sqliteDir);
    }

    await db.Database.MigrateAsync();
    logger.LogInformation("ClientService: database migrated.");

    if (!await db.Users.AnyAsync())
    {
        logger.LogInformation("ClientService: local DB is empty — pulling reference data from ServerService.");
        try
        {
            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var http = httpFactory.CreateClient("ServerService");
            var refData = await http.GetFromJsonAsync<SyncReferenceDataDto>("api/v1/sync/reference-data");

            if (refData is not null)
            {
                await db.Users.AddRangeAsync(
                    refData.Users.Select(u => new User { Id = u.Id, Username = u.Username, Email = u.Email }));

                await db.Buildings.AddRangeAsync(
                    refData.Buildings.Select(b => new Building { Id = b.Id, Identifier = b.Identifier }));

                await db.Rooms.AddRangeAsync(
                    refData.Rooms.Select(r => new Room { Id = r.Id, Identifier = r.Identifier, BuildingId = r.BuildingId }));

                await db.Surfaces.AddRangeAsync(
                    refData.Surfaces.Select(s => new Surface { Id = s.Id, Identifier = s.Identifier, RoomId = s.RoomId }));

                await db.Cells.AddRangeAsync(
                    refData.Cells.Select(c => new Cell { Id = c.Id, Identifier = c.Identifier, SurfaceId = c.SurfaceId }));

                await db.SaveChangesAsync();
                logger.LogInformation("ClientService: reference data loaded successfully.");
            }
        }
        catch (Exception ex)
        {
            // Log but do not crash — reference data pull failure is non-fatal for startup.
            logger.LogError(ex, "ClientService: failed to pull reference data from ServerService.");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    // AC1/AC2: return JSON {"status":"Healthy"} or {"status":"Unhealthy"} not plain-text
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
