using ClientService.Options;
using ClientService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ServerService.Controllers;
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
/// SQLite-compatible subclass of ServerDbContext for count unit tests.
/// Overrides RowVersion columns to ValueGeneratedNever so SQLite accepts
/// Array.Empty&lt;byte&gt;() as the default value (same pattern as MeasurementPullTests).
/// </summary>
internal sealed class TestableServerDbContextForCount : ServerDbContext
{
    public TestableServerDbContextForCount(DbContextOptions<ServerDbContext> options) : base(options) { }

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
/// Unit tests for Story 2.4: count endpoints on ServerService and ClientService.
/// </summary>
public class MeasurementCountTests : IDisposable
{
    // ── Server-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _serverConnection;
    private readonly TestableServerDbContextForCount _serverDb;

    // ── Client-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _clientConnection;
    private readonly ClientDbContext _clientDb;

    // ── Seeded FK IDs ──────────────────────────────────────────────────────────
    private Guid _seedUserId;
    private Guid _seedCellId;

    public MeasurementCountTests()
    {
        // Server DB
        _serverConnection = new SqliteConnection("DataSource=:memory:");
        _serverConnection.Open();
        var serverOptions = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(_serverConnection)
            .Options;
        _serverDb = new TestableServerDbContextForCount(serverOptions);
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

    private SyncMeasurementsController CreateServerController() =>
        new SyncMeasurementsController(
            _serverDb,
            MsOptions.Create(new SyncOptions { BatchSize = 5 }),
            NullLogger<SyncMeasurementsController>.Instance);

    private ClientService.Controllers.MeasurementsController CreateClientController(
        IHttpClientFactory? controllerFactory = null) =>
        new ClientService.Controllers.MeasurementsController(
            _clientDb,
            new MeasurementGenerationService(
                _clientDb,
                MsOptions.Create(new SyncOptions { MeasurementsPerClient = 10 }),
                MsOptions.Create(new ClientIdentityOptions
                {
                    UserId = new Guid("00000000-0000-0000-0000-000000000001")
                }),
                NullLogger<MeasurementGenerationService>.Instance),
            new MeasurementSyncService(
                _clientDb,
                new StubHttpClientFactoryThrowing(),
                MsOptions.Create(new SyncOptions { BatchSize = 5 }),
                MsOptions.Create(new ClientIdentityOptions { UserId = new Guid("00000000-0000-0000-0000-000000000001") }),
                NullLogger<MeasurementSyncService>.Instance),
            controllerFactory ?? new StubHttpClientFactoryThrowing(),
            NullLogger<ClientService.Controllers.MeasurementsController>.Instance);

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

    // ── Server-side Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task Count_WithMeasurements_ReturnsCorrectCount()
    {
        await SeedServerReferenceDataAsync();

        var measurements = Enumerable.Range(0, 8).Select(_ => new Measurement
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
        var result = await controller.Count(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(8, data.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Count_EmptyDatabase_ReturnsZero()
    {
        var controller = CreateServerController();
        var result = await controller.Count(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(0, data.GetProperty("count").GetInt32());
    }

    // ── Client-side Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task LocalCount_WithMeasurements_ReturnsCorrectCount()
    {
        await SeedClientReferenceDataAsync();

        var measurements = Enumerable.Range(0, 4).Select(_ => new Measurement
        {
            Id = Guid.NewGuid(),
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            SyncedAt = null,
            UserId = _seedUserId,
            CellId = _seedCellId
        }).ToList();
        _clientDb.Measurements.AddRange(measurements);
        await _clientDb.SaveChangesAsync();

        var controller = CreateClientController();
        var result = await controller.Count(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(4, data.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task LocalCount_EmptyDatabase_ReturnsZero()
    {
        var controller = CreateClientController();
        var result = await controller.Count(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(0, data.GetProperty("count").GetInt32());
    }

    // ── ServerCount Proxy Tests ───────────────────────────────────────────────

    [Fact]
    public async Task ServerCount_ServerReturnsOk_ReturnsServerJson()
    {
        var jsonPayload = "{\"count\":42}";
        var handler = new MockHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            }));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        var controller = CreateClientController(new StubHttpClientFactory(httpClient));

        var result = await controller.ServerCount(CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);
        var data = JsonSerializer.Deserialize<JsonElement>(content.Content!);
        Assert.Equal(42, data.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ServerCount_ServerReturnsNonOk_ReturnsProxiedStatusCode()
    {
        var handler = new MockHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"error\":\"down\"}", Encoding.UTF8, "application/json")
            }));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        var controller = CreateClientController(new StubHttpClientFactory(httpClient));

        var result = await controller.ServerCount(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }

    [Fact]
    public async Task ServerCount_HttpRequestThrows_Returns500()
    {
        var handler = new MockHttpMessageHandler(_ =>
            throw new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        var controller = CreateClientController(new StubHttpClientFactory(httpClient));

        var result = await controller.ServerCount(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
    }

    // ── Stub Helpers ──────────────────────────────────────────────────────────

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
