using ClientService.Models.Sync;
using Microsoft.EntityFrameworkCore;
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
    private readonly ILogger<MeasurementSyncService> _logger;

    public MeasurementSyncService(
        ClientDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<MeasurementSyncService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<MeasurementPushResult> PushAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: Load all unsynced measurements from local SQLite.
        var pending = await _db.Measurements
            .AsNoTracking()
            .Where(m => m.SyncedAt == null)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            _logger.LogInformation("MeasurementSyncService: no pending measurements to push.");
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
        var response = await http.PostAsJsonAsync(
            "api/v1/sync/measurements/push", pushRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "MeasurementSyncService: ServerService rejected push (HTTP {Status}): {Body}",
                (int)response.StatusCode, errorBody);
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
            "MeasurementSyncService: successfully pushed {Count} measurements; SyncedAt updated.",
            pending.Count);

        return new MeasurementPushResult(pending.Count,
            $"Pushed {pending.Count} measurements to ServerService.");
    }
}

public record MeasurementPushResult(int Count, string Message);
