using ClientService.Models.Sync;
using ClientService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ServerService.Controllers;
using ServerService.Models.Sync;
using Sync.Application.Options;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace MicroservicesSync.Tests.Measurements;

/// <summary>
/// SQLite-compatible subclass of ServerDbContext for pull unit tests.
/// Overrides RowVersion columns to ValueGeneratedNever so SQLite accepts
/// Array.Empty&lt;byte&gt;() as the default value (same pattern as MeasurementPushTests).
/// </summary>
internal sealed class TestableServerDbContextForPull : ServerDbContext
{
    public TestableServerDbContextForPull(DbContextOptions<ServerDbContext> options) : base(options) { }

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
/// Unit tests for Story 2.3: transactional batched client-side pull of consolidated measurements.
/// Server-side tests use TestableServerDbContextForPull.
/// Client-side tests use ClientDbContext in-memory SQLite.
/// </summary>
public class MeasurementPullTests : IDisposable
{
    // ── Server-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _serverConnection;
    private readonly TestableServerDbContextForPull _serverDb;

    // ── Client-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _clientConnection;
    private readonly ClientDbContext _clientDb;

    // ── Seeded FK IDs ──────────────────────────────────────────────────────────
    private Guid _seedUserId;
    private Guid _seedCellId;

    public MeasurementPullTests()
    {
        // Server DB
        _serverConnection = new SqliteConnection("DataSource=:memory:");
        _serverConnection.Open();
        var serverOptions = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(_serverConnection)
            .Options;
        _serverDb = new TestableServerDbContextForPull(serverOptions);
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

    private SyncMeasurementsController CreateServerController(int batchSize = 3) =>
        new SyncMeasurementsController(
            _serverDb,
            MsOptions.Create(new SyncOptions { BatchSize = batchSize }),
            NullLogger<SyncMeasurementsController>.Instance);

    private MeasurementSyncService CreateClientSyncService(int batchSize = 3,
        IHttpClientFactory? httpClientFactory = null) =>
        new MeasurementSyncService(
            _clientDb,
            httpClientFactory ?? new StubHttpClientFactoryThrowing(),
            MsOptions.Create(new SyncOptions { BatchSize = batchSize }),
            NullLogger<MeasurementSyncService>.Instance);

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

    private async Task SeedClientReferenceDataAsync()
    {
        _seedUserId = new Guid("00000000-0000-0000-0000-000000000001");
        var buildingId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var surfaceId = Guid.NewGuid();
        _seedCellId = Guid.NewGuid();

        _clientDb.Users.Add(new User { Id = _seedUserId, Username = "test-user", Email = "test@test.com" });
        _clientDb.Buildings.Add(new Building { Id = buildingId, Identifier = "test-building" });
        _clientDb.Rooms.Add(new Room { Id = roomId, Identifier = "test-room", BuildingId = buildingId });
        _clientDb.Surfaces.Add(new Surface { Id = surfaceId, Identifier = "test-surface", RoomId = roomId });
        _clientDb.Cells.Add(new Cell { Id = _seedCellId, Identifier = "test-cell-0", SurfaceId = surfaceId });

        await _clientDb.SaveChangesAsync();
    }

    private IHttpClientFactory BuildHttpClientFactory(List<ClientMeasurementPullItemDto> dtos)
    {
        var pullResponse = new ClientMeasurementPullResponse { Measurements = dtos, Total = dtos.Count };
        var json = JsonSerializer.Serialize(pullResponse);
        var handler = new MockHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        return new StubHttpClientFactory(httpClient);
    }

    private List<ClientMeasurementPullItemDto> BuildPullDtos(int count, Guid? userId = null, Guid? cellId = null) =>
        Enumerable.Range(0, count)
            .Select(_ => new ClientMeasurementPullItemDto
            {
                Id = Guid.NewGuid(),
                Value = 1.0m,
                RecordedAt = DateTime.UtcNow,
                UserId = userId ?? _seedUserId,
                CellId = cellId ?? _seedCellId
            })
            .ToList();

    // ── Server-side Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task Pull_WithMeasurements_ReturnsAllMeasurements()
    {
        await SeedServerReferenceDataAsync();

        // Insert 6 Measurement entities directly into the server DB.
        var measurements = Enumerable.Range(0, 6).Select(_ => new Measurement
        {
            Id = Guid.NewGuid(),
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            SyncedAt = null,
            UserId = _seedUserId,
            CellId = _seedCellId,
            RowVersion = Array.Empty<byte>()
        }).ToList();
        _serverDb.Measurements.AddRange(measurements);
        await _serverDb.SaveChangesAsync();

        var controller = CreateServerController();
        var result = await controller.Pull(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var response = JsonSerializer.Deserialize<MeasurementPullResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(response);
        Assert.Equal(6, response!.Total);
        Assert.Equal(6, response.Measurements.Count);
    }

    [Fact]
    public async Task Pull_EmptyDatabase_ReturnsEmptyResponse()
    {
        var controller = CreateServerController();
        var result = await controller.Pull(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var response = JsonSerializer.Deserialize<MeasurementPullResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(response);
        Assert.Equal(0, response!.Total);
        Assert.Empty(response.Measurements);
    }

    // ── Client-side Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task PullAsync_NewMeasurements_AllInsertedInLocalDb()
    {
        await SeedClientReferenceDataAsync();
        var dtos = BuildPullDtos(12);
        var factory = BuildHttpClientFactory(dtos);
        var service = CreateClientSyncService(batchSize: 3, httpClientFactory: factory);

        var result = await service.PullAsync();

        Assert.Equal(12, result.Count);
        Assert.Equal(12, await _clientDb.Measurements.CountAsync());
        var storedIds = await _clientDb.Measurements.Select(m => m.Id).ToListAsync();
        Assert.Equal(12, storedIds.Distinct().Count());
        foreach (var dto in dtos)
            Assert.Contains(dto.Id, storedIds);
    }

    [Fact]
    public async Task PullAsync_ExistingMeasurements_AreSkippedNotDuplicated()
    {
        await SeedClientReferenceDataAsync();

        // Pre-insert 5 measurements (simulate previously pushed measurements).
        var existingIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        _clientDb.Measurements.AddRange(existingIds.Select(id => new Measurement
        {
            Id = id,
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            UserId = _seedUserId,
            CellId = _seedCellId
        }));
        await _clientDb.SaveChangesAsync();

        // Server returns those 5 existing + 5 new GUIDs (10 total).
        var newDtos = BuildPullDtos(5);
        var allDtos = existingIds.Select(id => new ClientMeasurementPullItemDto
        {
            Id = id,
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            UserId = _seedUserId,
            CellId = _seedCellId
        }).Concat(newDtos).ToList();

        var factory = BuildHttpClientFactory(allDtos);
        var service = CreateClientSyncService(batchSize: 3, httpClientFactory: factory);

        var result = await service.PullAsync();

        Assert.Equal(5, result.Count); // Only 5 new ones inserted
        Assert.Equal(10, await _clientDb.Measurements.CountAsync()); // 5 pre-existing + 5 new
        var storedIds = await _clientDb.Measurements.Select(m => m.Id).ToListAsync();
        Assert.Equal(10, storedIds.Distinct().Count()); // No duplicates
    }

    [Fact]
    public async Task PullAsync_AllAlreadyPresent_ReturnsZeroCount()
    {
        await SeedClientReferenceDataAsync();

        // Pre-insert 5 measurements.
        var existingIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        _clientDb.Measurements.AddRange(existingIds.Select(id => new Measurement
        {
            Id = id,
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            UserId = _seedUserId,
            CellId = _seedCellId
        }));
        await _clientDb.SaveChangesAsync();

        // Server returns exactly those same 5 GUIDs.
        var sameDtos = existingIds.Select(id => new ClientMeasurementPullItemDto
        {
            Id = id,
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            UserId = _seedUserId,
            CellId = _seedCellId
        }).ToList();

        var factory = BuildHttpClientFactory(sameDtos);
        var service = CreateClientSyncService(batchSize: 3, httpClientFactory: factory);

        var result = await service.PullAsync();

        Assert.Equal(0, result.Count);
        Assert.Equal(5, await _clientDb.Measurements.CountAsync()); // No change
    }

    [Fact]
    public async Task PullAsync_TransactionRollsBack_WhenBatchFails()
    {
        // AC3: if any batch fails the entire pull is rolled back — table unchanged.
        // Strategy: send a response where duplicateId appears in both batch 0 and batch 1.
        // Batch 0 (indices 0-2) succeeds and inserts duplicateId inside the transaction.
        // Batch 1 (index 3) tries to AddRange duplicateId again → EF tracking conflict → exception.
        // RollbackAsync undoes batch 0's inserts → Measurements table stays empty.
        await SeedClientReferenceDataAsync();

        var duplicateId = Guid.NewGuid();
        var dtos = new List<ClientMeasurementPullItemDto>
        {
            // Batch 0 (indices 0-2)
            new() { Id = Guid.NewGuid(), Value = 1.0m, RecordedAt = DateTime.UtcNow, UserId = _seedUserId, CellId = _seedCellId },
            new() { Id = Guid.NewGuid(), Value = 1.0m, RecordedAt = DateTime.UtcNow, UserId = _seedUserId, CellId = _seedCellId },
            new() { Id = duplicateId,    Value = 1.0m, RecordedAt = DateTime.UtcNow, UserId = _seedUserId, CellId = _seedCellId },
            // Batch 1 (indices 3-5): duplicateId collides with batch 0 → PK/tracking conflict
            new() { Id = duplicateId,    Value = 2.0m, RecordedAt = DateTime.UtcNow, UserId = _seedUserId, CellId = _seedCellId },
            new() { Id = Guid.NewGuid(), Value = 1.0m, RecordedAt = DateTime.UtcNow, UserId = _seedUserId, CellId = _seedCellId },
            new() { Id = Guid.NewGuid(), Value = 1.0m, RecordedAt = DateTime.UtcNow, UserId = _seedUserId, CellId = _seedCellId },
        };

        var factory = BuildHttpClientFactory(dtos);
        var service = CreateClientSyncService(batchSize: 3, httpClientFactory: factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PullAsync());

        // Full rollback: no measurements should be in the DB
        Assert.Equal(0, await _clientDb.Measurements.CountAsync());
    }

    // ── Stub / Mock Helpers ───────────────────────────────────────────────────

    /// <summary>Throws if CreateClient is called — used when HTTP should not be invoked.</summary>
    private sealed class StubHttpClientFactoryThrowing : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("IHttpClientFactory.CreateClient should not be called in this test.");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
}
