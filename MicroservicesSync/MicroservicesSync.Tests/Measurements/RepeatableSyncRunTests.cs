using ClientService.Models.Sync;
using ClientService.Options;
using ClientService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
/// SQLite-compatible subclass of ServerDbContext for repeatability unit tests.
/// Overrides RowVersion columns to ValueGeneratedNever so SQLite accepts
/// Array.Empty&lt;byte&gt;() as the default value (same pattern as MeasurementPushTests).
/// </summary>
internal sealed class TestableServerDbContextForRepeatable : ServerDbContext
{
    public TestableServerDbContextForRepeatable(DbContextOptions<ServerDbContext> options) : base(options) { }

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
/// Unit tests for Story 2.5: repeatability of transactional sync runs from a clean baseline.
/// Validates that: (1) full reset → generate → push → pull cycles succeed multiple times,
/// (2) each new generate cycle produces fresh GUIDs that never collide with previous runs, and
/// (3) a client reset followed by a pull applies all fresh measurements without errors.
/// </summary>
public class RepeatableSyncRunTests : IDisposable
{
    // ── Server-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _serverConnection;
    private readonly TestableServerDbContextForRepeatable _serverDb;

    // ── Client-side SQLite in-memory ──────────────────────────────────────────
    private readonly SqliteConnection _clientConnection;
    private readonly ClientDbContext _clientDb;

    // ── Seeded FK IDs ──────────────────────────────────────────────────────────
    private Guid _seedUserId;
    private Guid _seedCellId;

    // ── Stable server-side GUIDs (from DatabaseSeeder — used by ResetServerAsync) ──
    private static readonly Guid ServerSeedUserId = new Guid("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ServerSeedCellId = new Guid("40000000-0000-0000-0000-000000000001");

    public RepeatableSyncRunTests()
    {
        // Server DB
        _serverConnection = new SqliteConnection("DataSource=:memory:");
        _serverConnection.Open();
        var serverOptions = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(_serverConnection)
            .Options;
        _serverDb = new TestableServerDbContextForRepeatable(serverOptions);
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

    private MeasurementGenerationService CreateGenerationService() =>
        new MeasurementGenerationService(
            _clientDb,
            MsOptions.Create(new SyncOptions { MeasurementsPerClient = 3 }),
            MsOptions.Create(new ClientIdentityOptions { UserId = _seedUserId }),
            NullLogger<MeasurementGenerationService>.Instance);

    private MeasurementSyncService CreateSyncService(IHttpClientFactory httpClientFactory) =>
        new MeasurementSyncService(
            _clientDb,
            httpClientFactory,
            MsOptions.Create(new SyncOptions { BatchSize = 2 }),
            NullLogger<MeasurementSyncService>.Instance);

    private async Task SeedReferenceDataAsync()
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

    /// <summary>
    /// Builds an IHttpClientFactory whose PushAsync mock returns a 200 OK and whose
    /// PullAsync mock returns the provided DTOs as a ClientMeasurementPullResponse JSON.
    /// </summary>
    private IHttpClientFactory BuildPushPullFactory(List<ClientMeasurementPullItemDto> pullDtos)
    {
        var pullResponse = new ClientMeasurementPullResponse { Measurements = pullDtos, Total = pullDtos.Count };
        var pullJson = JsonSerializer.Serialize(pullResponse);
        var pushSuccessJson = JsonSerializer.Serialize(new { count = pullDtos.Count, message = "OK" });

        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                // PushAsync: return 200 with push result JSON
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(pushSuccessJson, Encoding.UTF8, "application/json")
                });
            }
            // PullAsync: return 200 with pull response JSON
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pullJson, Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://server/") };
        return new StubHttpClientFactory(httpClient);
    }

    private List<ClientMeasurementPullItemDto> BuildPullDtos(List<Guid> ids) =>
        ids.Select(id => new ClientMeasurementPullItemDto
        {
            Id = id,
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            UserId = _seedUserId,
            CellId = _seedCellId
        }).ToList();

    // ── Test Cases ────────────────────────────────────────────────────────────

    /// <summary>
    /// AC#1 / AC#2: Two complete reset → generate → push → pull cycles both converge
    /// to the expected count with no exceptions across either run.
    /// </summary>
    [Fact]
    public async Task FullCycle_RunTwice_BothRunsConverge()
    {
        // ── Run 1 ─────────────────────────────────────────────────────────────
        // Seed client reference data and the server with its stable reference data (for FK validity)
        await SeedReferenceDataAsync();
        await DatabaseResetter.ResetServerAsync(_serverDb); // seeds Users/Cells with stable GUIDs
        // Guard: confirm stable-GUID constants match what DatabaseSeeder actually inserts.
        Assert.True(await _serverDb.Cells.AnyAsync(c => c.Id == ServerSeedCellId),
            "ServerSeedCellId must match the Cell GUID seeded by DatabaseSeeder (via ResetServerAsync).");
        var genService = CreateGenerationService();

        // Generate
        int run1Count = await genService.GenerateMeasurementsAsync();
        Assert.Equal(3, run1Count);

        // Capture IDs generated in Run 1
        var run1Ids = await _clientDb.Measurements.Select(m => m.Id).ToListAsync();
        Assert.Equal(3, run1Ids.Count);

        // Push
        var run1PullDtos = BuildPullDtos(run1Ids);
        var run1Factory = BuildPushPullFactory(run1PullDtos);
        var run1SyncService = CreateSyncService(run1Factory);

        var run1PushResult = await run1SyncService.PushAsync();
        Assert.Equal(3, run1PushResult.Count);

        // Simulate server storing the 3 measurements (use stable server-seeded GUIDs for FKs)
        _serverDb.Measurements.AddRange(run1Ids.Select(id => new Measurement
        {
            Id = id,
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            UserId = ServerSeedUserId,
            CellId = ServerSeedCellId,
            RowVersion = Array.Empty<byte>()
        }));
        await _serverDb.SaveChangesAsync();
        Assert.Equal(3, await _serverDb.Measurements.CountAsync());

        // Pull — client already has run1Ids locally (they were generated there);
        // TOCTOU guard skips all IDs the client itself generated: 0 new inserts expected.
        var run1PullResult = await run1SyncService.PullAsync();
        Assert.Equal(0, run1PullResult.Count); // Expected: TOCTOU guard skips client-generated IDs

        // ── Reset between runs ────────────────────────────────────────────────
        await DatabaseResetter.ResetClientAsync(_clientDb);
        await DatabaseResetter.ResetServerAsync(_serverDb); // re-seeds server reference data
        Assert.Equal(0, await _clientDb.Measurements.CountAsync());
        Assert.Equal(0, await _serverDb.Measurements.CountAsync());

        // Re-seed client reference data after reset
        await SeedReferenceDataAsync();

        // ── Run 2 ─────────────────────────────────────────────────────────────
        var genService2 = CreateGenerationService();
        int run2Count = await genService2.GenerateMeasurementsAsync();
        Assert.Equal(3, run2Count);

        var run2Ids = await _clientDb.Measurements.Select(m => m.Id).ToListAsync();
        Assert.Equal(3, run2Ids.Count);

        // GUIDs must differ from Run 1
        Assert.Empty(run1Ids.Intersect(run2Ids));

        // Push Run 2
        var run2PullDtos = BuildPullDtos(run2Ids);
        var run2Factory = BuildPushPullFactory(run2PullDtos);
        var run2SyncService = CreateSyncService(run2Factory);

        var run2PushResult = await run2SyncService.PushAsync();
        Assert.Equal(3, run2PushResult.Count);

        // Simulate server storing Run 2 measurements (stable server-seeded GUIDs for FKs)
        _serverDb.Measurements.AddRange(run2Ids.Select(id => new Measurement
        {
            Id = id,
            Value = 2.0m,
            RecordedAt = DateTime.UtcNow,
            UserId = ServerSeedUserId,
            CellId = ServerSeedCellId,
            RowVersion = Array.Empty<byte>()
        }));
        await _serverDb.SaveChangesAsync();
        Assert.Equal(3, await _serverDb.Measurements.CountAsync());

        // Pull — client already has run2Ids locally; TOCTOU guard skips them: 0 new inserts expected
        var run2PullResult = await run2SyncService.PullAsync();
        Assert.Equal(0, run2PullResult.Count); // Expected: TOCTOU guard skips client-generated IDs

        // Final assertion: both runs converged (3 local measurements after each run)
        Assert.Equal(3, await _clientDb.Measurements.CountAsync());
        Assert.Equal(3, await _serverDb.Measurements.CountAsync());
    }

    /// <summary>
    /// AC#2: After a client reset, a new generate cycle produces GUIDs that do not
    /// overlap with the previous cycle's GUIDs, guaranteeing zero duplicate-key errors on push.
    /// </summary>
    [Fact]
    public async Task NewMeasurementsAfterReset_HaveFreshGuids()
    {
        await SeedReferenceDataAsync();
        var svc = CreateGenerationService();

        // Generate first batch
        await svc.GenerateMeasurementsAsync();
        var set1 = (await _clientDb.Measurements.Select(m => m.Id).ToListAsync()).ToHashSet();
        Assert.Equal(3, set1.Count);

        // Reset the client DB
        await DatabaseResetter.ResetClientAsync(_clientDb);
        Assert.Equal(0, await _clientDb.Measurements.CountAsync());

        // Re-seed reference data (required before generating again)
        await SeedReferenceDataAsync();

        // Generate second batch
        var svc2 = CreateGenerationService();
        await svc2.GenerateMeasurementsAsync();
        var set2 = (await _clientDb.Measurements.Select(m => m.Id).ToListAsync()).ToHashSet();
        Assert.Equal(3, set2.Count);

        // No overlap — fresh GUIDs are guaranteed across runs
        Assert.False(set1.Overlaps(set2), "GUIDs from Run 2 must not overlap with Run 1.");
    }

    /// <summary>
    /// AC#1: After a client reset removes all measurements (including previously pulled ones),
    /// a fresh pull of new-GUID measurements inserts all of them without DbUpdateException.
    /// This validates that the TOCTOU guard in PullAsync does not block a legitimate re-pull
    /// after a reset.
    /// </summary>
    [Fact]
    public async Task PullAfterReset_AppliesAllNewMeasurements()
    {
        await SeedReferenceDataAsync();

        // Pre-populate the client DB with 3 measurements (simulating previously pulled data)
        var priorIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        _clientDb.Measurements.AddRange(priorIds.Select(id => new Measurement
        {
            Id = id,
            Value = 1.0m,
            RecordedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            UserId = _seedUserId,
            CellId = _seedCellId
        }));
        await _clientDb.SaveChangesAsync();
        Assert.Equal(3, await _clientDb.Measurements.CountAsync());

        // Reset the client DB
        await DatabaseResetter.ResetClientAsync(_clientDb);
        Assert.Equal(0, await _clientDb.Measurements.CountAsync());

        // Re-seed reference data after reset
        await SeedReferenceDataAsync();

        // Build fresh pull DTOs with new GUIDs (simulating a new cycle's server measurements)
        var freshIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var freshDtos = BuildPullDtos(freshIds);

        var factory = BuildPushPullFactory(freshDtos);
        var syncService = CreateSyncService(factory);

        // Pull should succeed and insert all 3 fresh measurements without exception
        var exception = await Record.ExceptionAsync(() => syncService.PullAsync());
        Assert.Null(exception);

        Assert.Equal(3, await _clientDb.Measurements.CountAsync());

        // Confirm the IDs stored are the fresh ones (not the prior ones)
        var storedIds = (await _clientDb.Measurements.Select(m => m.Id).ToListAsync()).ToHashSet();
        Assert.All(freshIds, id => Assert.Contains(id, storedIds));
        Assert.All(priorIds, id => Assert.DoesNotContain(id, storedIds));
    }

    // ── Stub / Mock Helpers ───────────────────────────────────────────────────

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
