using ClientService.Options;
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
            MsOptions.Create(new ClientIdentityOptions { UserId = new Guid("00000000-0000-0000-0000-000000000001") }),
            NullLogger<MeasurementSyncService>.Instance);

        var result = await syncService.PushAsync();

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public async Task Push_DuplicatePush_SkipsDuplicatesAndReturns200()
    {
        // AC#1 + AC#2: re-pushing the same IDs is idempotent — duplicates skipped,
        // HTTP 200 returned, DB count unchanged, response Pushed = 0.
        await SeedServerReferenceDataAsync();
        var controller = CreateController(batchSize: 3);
        var dtos = BuildDtos(6);
        var request = new MeasurementPushRequest { Measurements = dtos };

        // First push succeeds
        var first = await controller.Push(request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(first);
        Assert.Equal(6, await _serverDb.Measurements.CountAsync());

        // Second push with the same IDs succeeds (duplicates skipped) — no 500
        var second = await controller.Push(request, CancellationToken.None);
        var okSecond = Assert.IsType<OkObjectResult>(second);

        // AC#2: Pushed count must be 0 — all were duplicates
        var responseSecond = Assert.IsType<MeasurementPushResponse>(okSecond.Value);
        Assert.Equal(0, responseSecond.Pushed);

        // AC#1: count is still 6 — no duplicates inserted, no data lost
        Assert.Equal(6, await _serverDb.Measurements.CountAsync());
    }

    [Fact]
    public async Task Push_MixedNewAndDuplicate_OnlyNewInserted()
    {
        // AC#2: a mix of new and already-existing IDs — only new ones are inserted;
        // Pushed count reflects only newly inserted records.
        await SeedServerReferenceDataAsync();
        var controller = CreateController(batchSize: 10);

        // Push 3 measurements first
        var firstDtos = BuildDtos(3);
        var firstRequest = new MeasurementPushRequest { Measurements = firstDtos };
        var first = await controller.Push(firstRequest, CancellationToken.None);
        Assert.IsType<OkObjectResult>(first);
        Assert.Equal(3, await _serverDb.Measurements.CountAsync());

        // Second push: the same 3 (duplicates) + 4 new ones
        var mixedDtos = firstDtos.Concat(BuildDtos(4)).ToList();
        var mixedRequest = new MeasurementPushRequest { Measurements = mixedDtos };
        var second = await controller.Push(mixedRequest, CancellationToken.None);
        var okSecond = Assert.IsType<OkObjectResult>(second);

        // Only 4 new ones were inserted — DB has 3 + 4 = 7
        Assert.Equal(7, await _serverDb.Measurements.CountAsync());

        // AC#2: Pushed count = 4 (not 7)
        var response = Assert.IsType<MeasurementPushResponse>(okSecond.Value);
        Assert.Equal(4, response.Pushed);
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
