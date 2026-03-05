using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sync.Infrastructure.Data;

/// <summary>
/// Design-time factory for ClientDbContext, used by <c>dotnet ef migrations add</c>.
/// Creates a temporary SQLite file under the system temp directory for migration generation.
/// </summary>
public class ClientDbContextFactory : IDesignTimeDbContextFactory<ClientDbContext>
{
    public ClientDbContext CreateDbContext(string[] args)
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), "MicroservicesSyncDesignTime.sqlite");

        var optionsBuilder = new DbContextOptionsBuilder<ClientDbContext>();
        optionsBuilder.UseSqlite($"Data Source={tempDbPath}");

        return new ClientDbContext(optionsBuilder.Options);
    }
}
