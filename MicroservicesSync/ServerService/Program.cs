using Microsoft.EntityFrameworkCore;
using Sync.Application.Options;
using Sync.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register EF Core — SQL Server
builder.Services.AddDbContext<ServerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ServerDbContext>();

// Sync scenario options — configurable via SyncOptions__* env vars or appsettings.json
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));

var app = builder.Build();

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
    // AC1: return JSON {"status":"Healthy"} not plain-text
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
