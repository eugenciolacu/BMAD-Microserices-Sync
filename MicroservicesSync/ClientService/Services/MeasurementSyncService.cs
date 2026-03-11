using ClientService.Models.Sync;
using ClientService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sync.Application.Options;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using System.Net.Http.Json;

namespace ClientService.Services;

// NOTE (M2.2): MeasurementSyncService injects ClientDbContext directly (not via IMeasurementRepository)
// and IHttpClientFactory to call ServerService. This is a deliberate, acknowledged deviation from strict
// Application-layer separation, consistent with MeasurementGenerationService (NOTE M2.1) and
// AdminController (NOTE M4). Full repository abstraction is out of scope for Story 2.2.
public class MeasurementSyncService
{
    private readonly ClientDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly int _batchSize;
    private readonly Guid _userId;
    private readonly ILogger<MeasurementSyncService> _logger;

    public MeasurementSyncService(
        ClientDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<SyncOptions> syncOptions,
        IOptions<ClientIdentityOptions> clientIdentity,
        ILogger<MeasurementSyncService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _batchSize = syncOptions.Value.BatchSize;
        if (_batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(syncOptions),
                $"SyncOptions.BatchSize must be > 0 (was {_batchSize}).");
        _userId = clientIdentity.Value.UserId;
        _logger = logger;
    }

    public async Task<MeasurementPushResult> PushAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RunType"] = "push",
            ["UserId"] = _userId
        }))
        {
            // Step 1: Load all unsynced measurements from local SQLite.
            var pending = await _db.Measurements
                .AsNoTracking()
                .Where(m => m.SyncedAt == null)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0)
            {
                _logger.LogInformation(
                    "MeasurementSyncService: [{CorrelationId}] no pending measurements to push for user {UserId}.",
                    correlationId, _userId);
                return new MeasurementPushResult(0, "No pending measurements to push.");
            }

            // Step 2: Build push request DTO.
            var pushRequest = new ClientMeasurementPushRequest
            {
                Measurements = pending
                    .Select(m => new ClientMeasurementPushItemDto
                    {
                        Id = m.Id,
                        Value = m.Value,
                        RecordedAt = m.RecordedAt,
                        UserId = m.UserId,
                        CellId = m.CellId
                    })
                    .ToList()
            };

            // Step 3: POST to ServerService.
            var http = _httpClientFactory.CreateClient("ServerService");
            http.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId.ToString());
            var response = await http.PostAsJsonAsync(
                "api/v1/sync/measurements/push", pushRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "MeasurementSyncService: [{CorrelationId}] ServerService rejected push (HTTP {Status}): {Body}",
                    correlationId, (int)response.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"ServerService rejected push (HTTP {(int)response.StatusCode}): {errorBody}");
            }

            // Step 4: Mark pushed measurements as synced in local SQLite.
            // Uses ExecuteUpdateAsync (bulk SQL) to avoid EF change-tracking / ConcurrencyStamp complexity.
            var syncedAt = DateTime.UtcNow;
            var pushedIds = pending.Select(m => m.Id).ToList();

            await _db.Measurements
                .Where(m => pushedIds.Contains(m.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.SyncedAt, syncedAt),
                    cancellationToken);

            _logger.LogInformation(
                "MeasurementSyncService: [{CorrelationId}] successfully pushed {Count} measurements for user {UserId}; SyncedAt updated.",
                correlationId, pending.Count, _userId);

            return new MeasurementPushResult(pending.Count,
                $"Pushed {pending.Count} measurements to ServerService.");
        }
    }

    public async Task<MeasurementPullResult> PullAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RunType"] = "pull",
            ["UserId"] = _userId
        }))
        {
            // Step 1: GET consolidated measurement list from ServerService.
            var http = _httpClientFactory.CreateClient("ServerService");
            http.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId.ToString());
            var httpResponse = await http.GetAsync("api/v1/sync/measurements/pull", cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "MeasurementSyncService: [{CorrelationId}] ServerService rejected pull (HTTP {Status}): {Body}",
                    correlationId, (int)httpResponse.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"ServerService rejected pull (HTTP {(int)httpResponse.StatusCode}): {errorBody}");
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<ClientMeasurementPullResponse>(cancellationToken);

            if (response == null || response.Measurements.Count == 0)
            {
                _logger.LogInformation(
                    "MeasurementSyncService: [{CorrelationId}] pull returned 0 measurements from ServerService.",
                    correlationId);
                return new MeasurementPullResult(0, "No measurements available on ServerService.");
            }

            // Step 2: Open transaction first, then identify new IDs to eliminate TOCTOU race between
            // the existence check and the subsequent inserts.
            var serverIds = response.Measurements.Select(m => m.Id).ToHashSet();
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var existingIds = (await _db.Measurements
                        .AsNoTracking()
                        .Where(m => serverIds.Contains(m.Id))
                        .Select(m => m.Id)
                        .ToListAsync(cancellationToken))
                    .ToHashSet();

                var newMeasurements = response.Measurements
                    .Where(dto => !existingIds.Contains(dto.Id))
                    .ToList();

                if (newMeasurements.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation(
                        "MeasurementSyncService: [{CorrelationId}] all {Count} server measurements already present locally.",
                        correlationId, response.Measurements.Count);
                    return new MeasurementPullResult(0, "All measurements already present locally — no changes needed.");
                }

                // Step 3: Partition into in-memory batches; ALL applied in the single open transaction.
                var batches = newMeasurements
                    .Select((m, i) => new { m, i })
                    .GroupBy(x => x.i / _batchSize)
                    .Select(g => g.Select(x => x.m).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    var entities = batch.Select(dto => new Measurement
                    {
                        Id = dto.Id,
                        Value = dto.Value,
                        RecordedAt = dto.RecordedAt,
                        SyncedAt = null, // Pulled measurements: SyncedAt=null (not pushed BY this client)
                        UserId = dto.UserId,
                        CellId = dto.CellId
                    }).ToList();

                    await _db.Measurements.AddRangeAsync(entities, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "MeasurementSyncService: [{CorrelationId}] pulled and applied {Count} new measurements in {Batches} batch(es).",
                    correlationId, newMeasurements.Count, batches.Count);
                return new MeasurementPullResult(newMeasurements.Count,
                    $"Pulled {newMeasurements.Count} new measurements from ServerService in {batches.Count} batch(es).");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex,
                    "MeasurementSyncService: [{CorrelationId}] pull failed — local transaction rolled back.",
                    correlationId);
                throw new InvalidOperationException(
                    $"Pull failed — local transaction rolled back: {ex.Message}", ex);
            }
        }
    }
}

public record MeasurementPushResult(int Count, string Message);
public record MeasurementPullResult(int Count, string Message);
