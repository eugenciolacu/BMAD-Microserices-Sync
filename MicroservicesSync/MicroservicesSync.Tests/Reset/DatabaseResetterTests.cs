using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using Xunit;

namespace MicroservicesSync.Tests.Reset;

/// <summary>
/// SQLite-compatible subclass of ServerDbContext for unit testing.
/// Inherits all entity configurations from ServerDbContext (including FK relationships and indexes)
/// but overrides the RowVersion columns to ValueGeneratedNever so that SQLite can insert rows
/// with the client-supplied Array.Empty&lt;byte&gt;() default without a NOT NULL constraint failure.
/// ServerService production code always runs on SQL Server where IsRowVersion() works natively.
/// </summary>
internal sealed class TestableServerDbContext : ServerDbContext
{
    public TestableServerDbContext(DbContextOptions<ServerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Apply full ServerDbContext config including IsRowVersion()

        // Override IsRowVersion() (ValueGeneratedOnAddOrUpdate) → ValueGeneratedNever
        // so SQLite receives the explicit Array.Empty<byte>() value instead of expecting DB-generated.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entityType.FindProperty("RowVersion");
            if (rowVersionProp != null)
                modelBuilder.Entity(entityType.ClrType)
                    .Property("RowVersion")
                    .ValueGeneratedNever();
        }
    }
}

/// <summary>
/// Unit tests for DatabaseResetter — Story 1.5, AC#1 and AC#2.
/// Uses SQLite in-memory databases to exercise EF Core ExecuteDeleteAsync bulk deletes.
/// ServerDbContext: TestableServerDbContext (SQLite-compatible subclass, same schema).
/// ClientDbContext: SQLite in-memory (designed for SQLite natively).
/// </summary>
public class DatabaseResetterTests : IDisposable
{
    // ── SQLite in-memory connections (kept open for lifetime of each test) ────
    private readonly SqliteConnection _serverConnection;
    private readonly TestableServerDbContext _serverDb;

    private readonly SqliteConnection _clientConnection;
    private readonly ClientDbContext _clientDb;

    public DatabaseResetterTests()
    {
        // TestableServerDbContext — SQLite in-memory
        _serverConnection = new SqliteConnection("DataSource=:memory:");
        _serverConnection.Open();
        var serverOptions = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(_serverConnection)
            .Options;
        _serverDb = new TestableServerDbContext(serverOptions);
        _serverDb.Database.EnsureCreated();

        // ClientDbContext — SQLite in-memory
        _clientConnection = new SqliteConnection("DataSource=:memory:");
        _clientConnection.Open();
        var clientOptions = new DbContextOptionsBuilder<ClientDbContext>()
            .UseSqlite(_clientConnection)
            .Options;
        _clientDb = new ClientDbContext(clientOptions);
        _clientDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _serverDb.Dispose();
        _serverConnection.Dispose();
        _clientDb.Dispose();
        _clientConnection.Dispose();
    }

    // ── Helper: seed reference data + fake measurements into ServerDbContext ──

    private static async Task SeedServerWithMeasurementsAsync(ServerDbContext db)
    {
        await DatabaseSeeder.SeedAsync(db);

        // Add a fake measurement referencing seeded user and cell
        var userId = new Guid("00000000-0000-0000-0000-000000000001");
        var cellId = new Guid("40000000-0000-0000-0000-000000000001");

        await db.Measurements.AddRangeAsync(new[]
        {
            new Measurement { Id = Guid.NewGuid(), Value = 1.0m, RecordedAt = DateTime.UtcNow, UserId = userId, CellId = cellId },
            new Measurement { Id = Guid.NewGuid(), Value = 2.0m, RecordedAt = DateTime.UtcNow, UserId = userId, CellId = cellId },
        });
        await db.SyncRuns.AddRangeAsync(new[]
        {
            new SyncRun
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                RunType = "push",
                UserId = userId,
                MeasurementCount = 2,
                Status = "success"
            }
        });
        await db.SaveChangesAsync();
    }

    // ── Helper: seed reference data + fake measurements into ClientDbContext ──

    private static async Task SeedClientWithDataAsync(ClientDbContext db)
    {
        var userId = new Guid("00000000-0000-0000-0000-000000000001");
        var b1Id = new Guid("10000000-0000-0000-0000-000000000001");
        var r1Id = new Guid("20000000-0000-0000-0000-000000000001");
        var s1Id = new Guid("30000000-0000-0000-0000-000000000001");
        var cellId = new Guid("40000000-0000-0000-0000-000000000001");

        await db.Users.AddRangeAsync(new[]
        {
            new User { Id = userId, Username = "user1", Email = "u1@test.local" },
        });
        await db.Buildings.AddRangeAsync(new Building { Id = b1Id, Identifier = "b1" });
        await db.Rooms.AddRangeAsync(new Room { Id = r1Id, Identifier = "r1", BuildingId = b1Id });
        await db.Surfaces.AddRangeAsync(new Surface { Id = s1Id, Identifier = "s1", RoomId = r1Id });
        await db.Cells.AddRangeAsync(new Cell { Id = cellId, Identifier = "c1", SurfaceId = s1Id });
        await db.SaveChangesAsync();

        await db.Measurements.AddRangeAsync(new[]
        {
            new Measurement { Id = Guid.NewGuid(), Value = 5.0m, RecordedAt = DateTime.UtcNow, UserId = userId, CellId = cellId },
            new Measurement { Id = Guid.NewGuid(), Value = 6.0m, RecordedAt = DateTime.UtcNow, UserId = userId, CellId = cellId },
        });
        await db.SaveChangesAsync();
    }

    // ── ResetServerAsync tests ────────────────────────────────────────────────

    [Fact]
    public async Task ResetServerAsync_DeletesMeasurements_AndReseeds_ReferenceData()
    {
        // Arrange: seed reference data + measurements
        await SeedServerWithMeasurementsAsync(_serverDb);
        Assert.Equal(2, await _serverDb.Measurements.CountAsync());
        Assert.Equal(5, await _serverDb.Users.CountAsync());
        Assert.Equal(1, await _serverDb.SyncRuns.CountAsync());

        // Act
        await DatabaseResetter.ResetServerAsync(_serverDb);

        // Assert: measurements gone, reference data re-seeded with correct counts
        Assert.Equal(0, await _serverDb.Measurements.CountAsync());
        Assert.Equal(5, await _serverDb.Users.CountAsync());
        Assert.Equal(2, await _serverDb.Buildings.CountAsync());
        Assert.Equal(4, await _serverDb.Rooms.CountAsync());
        Assert.Equal(8, await _serverDb.Surfaces.CountAsync());
        Assert.Equal(16, await _serverDb.Cells.CountAsync());
        Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());
    }

    [Fact]
    public async Task ResetServerAsync_Is_Idempotent()
    {
        // Arrange: seed and add measurements
        await SeedServerWithMeasurementsAsync(_serverDb);
        Assert.Equal(1, await _serverDb.SyncRuns.CountAsync());

        // Act: reset twice
        await DatabaseResetter.ResetServerAsync(_serverDb);
        await DatabaseResetter.ResetServerAsync(_serverDb);

        // Assert: same clean state after second reset
        Assert.Equal(0, await _serverDb.Measurements.CountAsync());
        Assert.Equal(5, await _serverDb.Users.CountAsync());
        Assert.Equal(2, await _serverDb.Buildings.CountAsync());
        Assert.Equal(4, await _serverDb.Rooms.CountAsync());
        Assert.Equal(8, await _serverDb.Surfaces.CountAsync());
        Assert.Equal(16, await _serverDb.Cells.CountAsync());
        Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());
    }

    [Fact]
    public async Task ResetServerAsync_OnEmptyDatabase_SeedsReferenceData()
    {
        // Arrange: ensure DB is empty
        Assert.Equal(0, await _serverDb.Users.CountAsync());

        // Act
        await DatabaseResetter.ResetServerAsync(_serverDb);

        // Assert: reference data is created even from an empty database
        Assert.Equal(5, await _serverDb.Users.CountAsync());
        Assert.Equal(2, await _serverDb.Buildings.CountAsync());
        Assert.Equal(16, await _serverDb.Cells.CountAsync());
        Assert.Equal(0, await _serverDb.SyncRuns.CountAsync());
    }

    // ── ResetClientAsync tests ────────────────────────────────────────────────

    [Fact]
    public async Task ResetClientAsync_ClearsAllTables_NoReseeding()
    {
        // Arrange: seed reference data + measurements
        await SeedClientWithDataAsync(_clientDb);
        Assert.Equal(2, await _clientDb.Measurements.CountAsync());
        Assert.Equal(1, await _clientDb.Users.CountAsync());

        // Act
        await DatabaseResetter.ResetClientAsync(_clientDb);

        // Assert: all tables empty (no re-seeding on client)
        Assert.Equal(0, await _clientDb.Measurements.CountAsync());
        Assert.Equal(0, await _clientDb.Users.CountAsync());
        Assert.Equal(0, await _clientDb.Buildings.CountAsync());
        Assert.Equal(0, await _clientDb.Rooms.CountAsync());
        Assert.Equal(0, await _clientDb.Surfaces.CountAsync());
        Assert.Equal(0, await _clientDb.Cells.CountAsync());
    }

    [Fact]
    public async Task ResetClientAsync_Is_Idempotent()
    {
        // Arrange: seed data
        await SeedClientWithDataAsync(_clientDb);

        // Act: reset twice
        await DatabaseResetter.ResetClientAsync(_clientDb);
        await DatabaseResetter.ResetClientAsync(_clientDb);

        // Assert: still all empty after second reset
        Assert.Equal(0, await _clientDb.Measurements.CountAsync());
        Assert.Equal(0, await _clientDb.Users.CountAsync());
    }

    [Fact]
    public async Task ResetClientAsync_OnEmptyDatabase_RemainsEmpty()
    {
        // Arrange: ensure DB is empty
        Assert.Equal(0, await _clientDb.Users.CountAsync());

        // Act: reset on empty DB should not throw
        await DatabaseResetter.ResetClientAsync(_clientDb);

        // Assert: still empty
        Assert.Equal(0, await _clientDb.Users.CountAsync());
        Assert.Equal(0, await _clientDb.Measurements.CountAsync());
    }
}
