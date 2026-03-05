using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sync.Infrastructure.Data;

/// <summary>
/// Design-time factory for ServerDbContext, used by <c>dotnet ef migrations add</c>.
/// Reads the connection string from the DESIGN_TIME_SERVER_CONNECTION environment variable,
/// or falls back to a local SQL Server default for developer workstations.
/// </summary>
public class ServerDbContextFactory : IDesignTimeDbContextFactory<ServerDbContext>
{
    public ServerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DESIGN_TIME_SERVER_CONNECTION")
            ?? "Server=localhost,1433;Database=MicroservicesSyncDev;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<ServerDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ServerDbContext(optionsBuilder.Options);
    }
}
