using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using ServerService.Data;
using ServerService.Middleware;
using ServerService.Repositories;
using ServerService.Services;

namespace ServerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Create a bootstrap logger for startup errors
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                // log application start in console
                Log.Information("Starting application...");

                var builder = WebApplication.CreateBuilder(args);

                // Configure Serilog from appsettings.json
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext());

                // Add services to the container
                builder.Services.AddControllersWithViews();

                // Configure DbContext with SQL Server
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

                // Register AutoMapper
                builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

                // Register generic repository
                builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

                // Register generic service
                builder.Services.AddScoped(typeof(IGenericService<,,,>), typeof(GenericService<,,,>));

                // Configure OpenAPI
                builder.Services.AddOpenApi();

                var app = builder.Build();

                // Log application start after build in file
                Log.Information("Starting application...");

                // Apply migrations on startup for all environments
                try
                {
                    using (var scope = app.Services.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        Log.Information("Applying database migrations...");
                        dbContext.Database.Migrate();
                        Log.Information("Database migrations applied successfully.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while migrating the database.");
                    throw;
                }

                // Configure exception handler
                app.ConfigureExceptionHandler();

                if (app.Environment.IsDevelopment())
                {
                    app.MapOpenApi();
                    app.MapScalarApiReference();

                    // Development: redirect to API documentation
                    app.MapGet("/", () => Results.Redirect("/scalar/v1"))
                        .ExcludeFromDescription();
                }

                // On docker
                // Only use HTTPS redirection in production with proper certificates
                // Disable for Docker to avoid port binding issues
                // app.UseHttpsRedirection();

                app.UseStaticFiles();

                app.UseAuthorization();

                app.MapControllers();

                // Production: redirect to Home page
                if (app.Environment.IsProduction())
                {
                    app.MapGet("/", () => Results.Redirect("/home"))
                        .ExcludeFromDescription();
                }

                Log.Information("Application started successfully");

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application failed to start");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
