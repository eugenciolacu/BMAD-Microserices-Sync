using Microsoft.EntityFrameworkCore;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data.Configurations;

namespace Sync.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for SQLite (ClientService).
/// Uses the same shared entity configurations as ServerDbContext, but substitutes
/// SQL Server rowversion with a shadow long ConcurrencyStamp for SQLite compatibility.
/// </summary>
public class ClientDbContext : DbContext
{
    private const string RowVersionPropertyName = "RowVersion";

    public ClientDbContext(DbContextOptions<ClientDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Surface> Surfaces => Set<Surface>();
    public DbSet<Cell> Cells => Set<Cell>();
    public DbSet<Measurement> Measurements => Set<Measurement>();

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

        // SQLite-specific: ignore RowVersion (not supported) and add a shadow long
        // ConcurrencyStamp for each entity that would have had RowVersion.
        // Increment ConcurrencyStamp manually before SaveChangesAsync in write operations.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entityType.FindProperty(RowVersionPropertyName);
            if (rowVersionProp != null)
                modelBuilder.Entity(entityType.ClrType).Ignore(RowVersionPropertyName);

            modelBuilder.Entity(entityType.ClrType)
                .Property<long>("ConcurrencyStamp")
                .IsConcurrencyToken()
                .HasDefaultValue(0L);
        }
    }
}
