using ClientService.Options;
using ClientService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sync.Application.Options;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace MicroservicesSync.Tests.Measurements;

public class MeasurementGenerationTests : IDisposable
{
    private readonly SqliteConnection _clientConnection;
    private readonly ClientDbContext _clientDb;

    public MeasurementGenerationTests()
    {
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
        _clientDb.Dispose();
        _clientConnection.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private MeasurementGenerationService CreateService(int measurementsPerClient = 10, Guid? userId = null)
    {
        var syncOpts = MsOptions.Create(new SyncOptions { MeasurementsPerClient = measurementsPerClient });
        var identityOpts = MsOptions.Create(new ClientIdentityOptions
        {
            UserId = userId ?? new Guid("00000000-0000-0000-0000-000000000001")
        });
        var logger = NullLogger<MeasurementGenerationService>.Instance;
        return new MeasurementGenerationService(_clientDb, syncOpts, identityOpts, logger);
    }

    private async Task SeedCellsAsync(int count = 5)
    {
        // Seed the user that measurements will reference via UserId FK
        _clientDb.Users.Add(new User
        {
            Id = new Guid("00000000-0000-0000-0000-000000000001"),
            Username = "test-user",
            Email = "test@test.com"
        });

        var buildingId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var surfaceId = Guid.NewGuid();

        _clientDb.Buildings.Add(new Building { Id = buildingId, Identifier = "test-building" });
        _clientDb.Rooms.Add(new Room { Id = roomId, Identifier = "test-room", BuildingId = buildingId });
        _clientDb.Surfaces.Add(new Surface { Id = surfaceId, Identifier = "test-surface", RoomId = roomId });
        for (int i = 0; i < count; i++)
            _clientDb.Cells.Add(new Cell { Id = Guid.NewGuid(), Identifier = $"test-cell-{i}", SurfaceId = surfaceId });

        await _clientDb.SaveChangesAsync();
    }

    // ── Test Cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateMeasurements_DefaultConfig_CreatesCorrectCount()
    {
        await SeedCellsAsync(5);
        var svc = CreateService(measurementsPerClient: 10);

        var result = await svc.GenerateMeasurementsAsync();

        Assert.Equal(10, result);
        Assert.Equal(10, await _clientDb.Measurements.CountAsync());
    }

    [Fact]
    public async Task GenerateMeasurements_AllHaveCorrectUserId()
    {
        var configuredUserId = new Guid("00000000-0000-0000-0000-000000000001");
        await SeedCellsAsync(5);
        var svc = CreateService(userId: configuredUserId);

        await svc.GenerateMeasurementsAsync();

        var measurements = await _clientDb.Measurements.ToListAsync();
        Assert.All(measurements, m => Assert.Equal(configuredUserId, m.UserId));
    }

    [Fact]
    public async Task GenerateMeasurements_AllHaveSyncedAtNull()
    {
        await SeedCellsAsync(5);
        var svc = CreateService();

        await svc.GenerateMeasurementsAsync();

        var measurements = await _clientDb.Measurements.ToListAsync();
        Assert.All(measurements, m => Assert.Null(m.SyncedAt));
    }

    [Fact]
    public async Task GenerateMeasurements_AllHaveRecordedAtUtc()
    {
        await SeedCellsAsync(5);
        var svc = CreateService();
        var before = DateTime.UtcNow;

        await svc.GenerateMeasurementsAsync();

        var after = DateTime.UtcNow.AddSeconds(5);
        var measurements = await _clientDb.Measurements.ToListAsync();
        Assert.All(measurements, m =>
        {
            Assert.True(m.RecordedAt >= before.AddSeconds(-5), $"RecordedAt {m.RecordedAt} is before window start");
            Assert.True(m.RecordedAt <= after, $"RecordedAt {m.RecordedAt} is after window end");
        });
    }

    [Fact]
    public async Task GenerateMeasurements_ValueInExpectedRange()
    {
        await SeedCellsAsync(5);
        var svc = CreateService();

        await svc.GenerateMeasurementsAsync();

        var measurements = await _clientDb.Measurements.ToListAsync();
        Assert.All(measurements, m =>
        {
            Assert.True(m.Value >= 0m, $"Value {m.Value} is below 0");
            Assert.True(m.Value <= 100m, $"Value {m.Value} exceeds 100");
        });
    }

    [Fact]
    public async Task GenerateMeasurements_NoCells_ThrowsInvalidOperation()
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GenerateMeasurementsAsync());
    }

    [Fact]
    public async Task GenerateMeasurements_CustomCount_RespectsConfig()
    {
        await SeedCellsAsync(5);
        var svc = CreateService(measurementsPerClient: 3);

        var result = await svc.GenerateMeasurementsAsync();

        Assert.Equal(3, result);
        Assert.Equal(3, await _clientDb.Measurements.CountAsync());
    }
}
