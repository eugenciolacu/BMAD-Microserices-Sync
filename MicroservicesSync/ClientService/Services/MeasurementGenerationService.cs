using ClientService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sync.Application.Options;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;

namespace ClientService.Services;

// NOTE (M2.1): MeasurementGenerationService injects ClientDbContext directly rather than via an
// Application-layer repository or service. This is a deliberate, acknowledged deviation from the
// project-context layering rule (Web → Application → Domain only) for this ClientService-specific
// generation concern. The full IMeasurementRepository / IRepository<T> abstraction layer is Story 2.2+
// scope. Consistent with AdminController's direct-DbContext pattern (// NOTE (M4):).
public class MeasurementGenerationService
{
    private readonly ClientDbContext _db;
    private readonly SyncOptions _syncOptions;
    private readonly Guid _userId;
    private readonly ILogger<MeasurementGenerationService> _logger;

    public MeasurementGenerationService(
        ClientDbContext db,
        IOptions<SyncOptions> syncOptions,
        IOptions<ClientIdentityOptions> clientIdentity,
        ILogger<MeasurementGenerationService> logger)
    {
        _db = db;
        _syncOptions = syncOptions.Value;
        _userId = clientIdentity.Value.UserId;
        _logger = logger;
    }

    public async Task<int> GenerateMeasurementsAsync(CancellationToken cancellationToken = default)
    {
        var cellIds = await _db.Cells
            .AsNoTracking()
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (cellIds.Count == 0)
            throw new InvalidOperationException(
                "No cells found in local DB. Pull reference data from ServerService before generating measurements.");

        var now = DateTime.UtcNow;
        var count = _syncOptions.MeasurementsPerClient;
        var measurements = Enumerable.Range(0, count)
            .Select(i => new Measurement
            {
                Id = Guid.NewGuid(),
                Value = Math.Round((decimal)(Random.Shared.NextDouble() * 100), 2),
                RecordedAt = now,
                SyncedAt = null,
                UserId = _userId,
                CellId = cellIds[i % cellIds.Count]
            })
            .ToList();

        await _db.Measurements.AddRangeAsync(measurements, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "MeasurementGenerationService: generated {Count} measurements for UserId {UserId}.",
            count, _userId);

        return count;
    }
}
