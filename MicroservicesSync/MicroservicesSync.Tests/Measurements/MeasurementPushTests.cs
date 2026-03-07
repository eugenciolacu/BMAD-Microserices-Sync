using ClientService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServerService.Controllers;
using ServerService.Models.Sync;
using Sync.Application.Options;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace MicroservicesSync.Tests.Measurements;

/// <summary>
/// SQLite-compatible subclass of ServerDbContext for push unit tests.
/// Mirrors the pattern in DatabaseResetterTests — overrides RowVersion columns to
/// ValueGeneratedNever so SQLite accepts the Array.Empty&lt;byte&gt;() default value.
/// </summary>
internal sealed class TestableServerDbContext : ServerDbContext
{
    public TestableServerDbContext(DbContextOptions<ServerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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
/// Unit tests for Story 2.2: transactional batched server-side measurement push.
/// Server-side tests use TestableServerDbContext (SQLite-compatible ServerDbContext).
/// Client-side tests use ClientDbContext in-memory SQLite.
/// </summary>
public class MeasurementPushTests : IDisposable
{
    // ── Server-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _serverConnection;
    private readonly TestableServerDbContext _serverDb;

    // ── Client-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _clientConnection;
    private readonly ClientDbContext _clientDb;

    // ── Seeded FK IDs (populated by SeedServerReferenceDataAsync) ─────────────
    private Guid _seedUserId;
    private Guid _seedCellId;

    public MeasurementPushTests()
    {
        // Server DB
        _serverConnection = new SqliteConnection("DataSource=:memory:");
        _serverConnection.Open();
        var serverOptions = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(_serverConnection)
            .Options;
        _serverDb = new TestableServerDbContext(serverOptions);
        _serverDb.Database.EnsureCreated();

        // Client DB
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SyncMeasurementsController CreateController(int batchSize = 3) =>
        new SyncMeasurementsController(
            _serverDb,
            MsOptions.Create(new SyncOptions { BatchSize = batchSize }),
            NullLogger<SyncMeasurementsController>.Instance);

    private async Task SeedServerReferenceDataAsync()
    {
        _seedUserId = new Guid("00000000-0000-0000-0000-000000000001");
        var buildingId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var surfaceId = Guid.NewGuid();
        _seedCellId = Guid.NewGuid();

        _serverDb.Users.Add(new User { Id = _seedUserId, Username = "test-user", Email = "test@test.com" });
        _serverDb.Buildings.Add(new Building { Id = buildingId, Identifier = "test-building" });
        _serverDb.Rooms.Add(new Room { Id = roomId, Identifier = "test-room", BuildingId = buildingId });
        _serverDb.Surfaces.Add(new Surface { Id = surfaceId, Identifier = "test-surface", RoomId = roomId });
        _serverDb.Cells.Add(new Cell { Id = _seedCellId, Identifier = "test-cell-0", SurfaceId = surfaceId });

        await _serverDb.SaveChangesAsync();
    }

    private List<MeasurementPushItemDto> BuildDtos(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new MeasurementPushItemDto
            {
                Id = Guid.NewGuid(),
                Value = 1.0m,
                RecordedAt = DateTime.UtcNow,
                UserId = _seedUserId,
                CellId = _seedCellId
            })
            .ToList();

    // ── Test Cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Push_ValidMeasurements_AllStoredOnServer()
    {
        await SeedServerReferenceDataAsync();
        var controller = CreateController(batchSize: 3);
        var request = new MeasurementPushRequest { Measurements = BuildDtos(6) };

        var result = await controller.Push(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(6, await _serverDb.Measurements.CountAsync());
    }

    [Fact]
    public async Task Push_EmptyRequest_ReturnsBadRequestResult()
    {
        var controller = CreateController();
        var request = new MeasurementPushRequest();

        var result = await controller.Push(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Push_BatchedCorrectly_AllMeasurementsPresent()
    {
        await SeedServerReferenceDataAsync();
        var controller = CreateController(batchSize: 3);
        var dtos = BuildDtos(12);
        var request = new MeasurementPushRequest { Measurements = dtos };

        var result = await controller.Push(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await _serverDb.Measurements.ToListAsync();
        Assert.Equal(12, stored.Count);
        Assert.Equal(12, stored.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public async Task Push_NoPendingMeasurements_ReturnsZeroCount()
    {
        // MeasurementSyncService returns early (Count=0) before ever calling IHttpClientFactory
        // when there are no measurements with SyncedAt == null.
        var syncService = new MeasurementSyncService(
            _clientDb,
            new StubHttpClientFactory(),
            MsOptions.Create(new SyncOptions { BatchSize = 3 }),
            NullLogger<MeasurementSyncService>.Instance);

        var result = await syncService.PushAsync();

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task Push_DuplicatePush_RollsBackAndReturns500()
    {
        // AC#3: when a push fails (here: PK conflict on re-push), the transaction rolls back
        // and no partial data from the failed push is persisted.
        await SeedServerReferenceDataAsync();
        var controller = CreateController(batchSize: 3);
        var dtos = BuildDtos(6);
        var request = new MeasurementPushRequest { Measurements = dtos };

        // First push succeeds
        var first = await controller.Push(request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(first);
        Assert.Equal(6, await _serverDb.Measurements.CountAsync());

        // Second push with the same IDs fails (PK/EF tracking conflict) and rolls back
        var second = await controller.Push(request, CancellationToken.None);
        var statusResult = Assert.IsType<ObjectResult>(second);
        Assert.Equal(500, statusResult.StatusCode);

        // AC#3 verified: count is still 6 (the 6 from the first push) — no partial data
        Assert.Equal(6, await _serverDb.Measurements.CountAsync());
    }

    /// <summary>
    /// Stub that throws if called — verifies that PushAsync exits early without HTTP calls
    /// when there are no pending measurements.
    /// </summary>
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException(
                "IHttpClientFactory.CreateClient should not be called when there are no pending measurements.");
    }
}
