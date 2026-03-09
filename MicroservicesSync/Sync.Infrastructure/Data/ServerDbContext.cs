using Microsoft.EntityFrameworkCore;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data.Configurations;

namespace Sync.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for SQL Server (ServerService).
/// Uses shared entity configurations + SQL Server rowversion concurrency tokens.
/// </summary>
public class ServerDbContext : DbContext
{
    private const string RowVersionPropertyName = "RowVersion";

    public ServerDbContext(DbContextOptions<ServerDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Surface> Surfaces => Set<Surface>();
    public DbSet<Cell> Cells => Set<Cell>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply shared structural configurations (keys, indexes, FK relationships, precision).
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new BuildingConfiguration());
        modelBuilder.ApplyConfiguration(new RoomConfiguration());
        modelBuilder.ApplyConfiguration(new SurfaceConfiguration());
        modelBuilder.ApplyConfiguration(new CellConfiguration());
        modelBuilder.ApplyConfiguration(new MeasurementConfiguration());
        modelBuilder.ApplyConfiguration(new SyncRunConfiguration());

        // SQL Server-specific: configure RowVersion as a rowversion/timestamp column
        // for all entities that expose the byte[] RowVersion property.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entityType.FindProperty(RowVersionPropertyName);
            if (rowVersionProp != null)
                modelBuilder.Entity(entityType.ClrType)
                    .Property(RowVersionPropertyName)
                    .IsRowVersion();
        }
    }
}
