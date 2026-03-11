using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServerService.Models.Sync;
using Sync.Application.Options;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;

namespace ServerService.Controllers;

[ApiController]
[Route("api/v1/sync")]
public class SyncMeasurementsController : ControllerBase
{
    private readonly ServerDbContext _db;
    private readonly int _batchSize;
    private readonly ILogger<SyncMeasurementsController> _logger;

    public SyncMeasurementsController(
        ServerDbContext db,
        IOptions<SyncOptions> syncOptions,
        ILogger<SyncMeasurementsController> logger)
    {
        _db = db;
        _batchSize = syncOptions.Value.BatchSize;
        if (_batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(syncOptions),
                $"SyncOptions.BatchSize must be > 0 (was {_batchSize}).");
        _logger = logger;
    }

    [HttpPost("measurements/push")]
    public async Task<IActionResult> Push(
        [FromBody] MeasurementPushRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Measurements.Count == 0)
            return BadRequest(new { message = "No measurements provided." });

        var syncRunId = Guid.NewGuid();
        var userId = request.Measurements.FirstOrDefault()?.UserId;
        var clientCorrelationId = Request.Headers["X-Correlation-Id"].FirstOrDefault();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["SyncRunId"] = syncRunId,
            ["RunType"] = "push",
            ["UserId"] = userId,
            ["ClientCorrelationId"] = clientCorrelationId
        }))
        {
            // Partition into in-memory batches; ALL processed in a single transaction.
            var batches = request.Measurements
                .Select((m, i) => new { m, i })
                .GroupBy(x => x.i / _batchSize)
                .Select(g => g.Select(x => x.m).ToList())
                .ToList();

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var batch in batches)
                {
                    var entities = batch.Select(dto => new Measurement
                    {
                        Id = dto.Id,
                        Value = dto.Value,
                        RecordedAt = dto.RecordedAt,
                        SyncedAt = null,
                        UserId = dto.UserId,
                        CellId = dto.CellId
                    }).ToList();

                    await _db.Measurements.AddRangeAsync(entities, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "SyncMeasurementsController: [{SyncRunId}] pushed {Count} measurements from user {UserId} in {Batches} batch(es).",
                    syncRunId, request.Measurements.Count, userId, batches.Count);

                // Record sync run summary (best-effort; separate from measurement transaction).
                // Wrapped in its own try/catch: if this insert fails the measurements are already
                // committed — we must NOT rollback or return 500.
                try
                {
                    var syncRun = new Sync.Domain.Entities.SyncRun
                    {
                        Id = syncRunId,
                        OccurredAt = DateTime.UtcNow,
                        RunType = "push",
                        UserId = request.Measurements.First().UserId,
                        MeasurementCount = request.Measurements.Count,
                        Status = "success"
                    };
                    _db.SyncRuns.Add(syncRun);
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx,
                        "SyncMeasurementsController: failed to record push SyncRun — measurements were committed.");
                }

                return Ok(new MeasurementPushResponse
                {
                    Pushed = request.Measurements.Count,
                    Message = $"Pushed {request.Measurements.Count} measurements in {batches.Count} batch(es)."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex,
                    "SyncMeasurementsController: [{SyncRunId}] push failed for user {UserId} — transaction rolled back.",
                    syncRunId, userId);

                // Record failed sync run (best-effort; swallow exceptions)
                try
                {
                    var failedRun = new Sync.Domain.Entities.SyncRun
                    {
                        Id = syncRunId,
                        OccurredAt = DateTime.UtcNow,
                        RunType = "push",
                        UserId = request.Measurements.FirstOrDefault()?.UserId,
                        MeasurementCount = 0,
                        Status = "failed",
                        ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message
                    };
                    _db.SyncRuns.Add(failedRun);
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                catch { /* best-effort; do not mask the original error */ }

                return StatusCode(500, new { message = "Push failed. Transaction rolled back.", error = ex.Message });
            }
        }
    }

    [HttpGet("measurements/count")]
    public async Task<IActionResult> Count(CancellationToken cancellationToken)
    {
        var count = await _db.Measurements
            .AsNoTracking()
            .CountAsync(cancellationToken);
        _logger.LogInformation(
            "SyncMeasurementsController: count requested — {Count} measurements.", count);
        return Ok(new { count });
    }

    [HttpGet("measurements/pull")]
    public async Task<IActionResult> Pull(CancellationToken cancellationToken)
    {
        var syncRunId = Guid.NewGuid();
        var clientCorrelationId = Request.Headers["X-Correlation-Id"].FirstOrDefault();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["SyncRunId"] = syncRunId,
            ["RunType"] = "pull",
            ["ClientCorrelationId"] = clientCorrelationId
        }))
        {
            try
            {
                var measurements = await _db.Measurements
                    .AsNoTracking()
                    .Select(m => new MeasurementPullItemDto
                    {
                        Id = m.Id,
                        Value = m.Value,
                        RecordedAt = m.RecordedAt,
                        UserId = m.UserId,
                        CellId = m.CellId
                    })
                    .ToListAsync(cancellationToken);

                _logger.LogInformation(
                    "SyncMeasurementsController: [{SyncRunId}] pull completed — returning {Count} measurements.",
                    syncRunId, measurements.Count);

                // Record pull run summary (best-effort; use CancellationToken.None so a client
                // disconnect after data is fetched cannot cancel this record and cause a false failure).
                try
                {
                    var syncRun = new Sync.Domain.Entities.SyncRun
                    {
                        Id = syncRunId,
                        OccurredAt = DateTime.UtcNow,
                        RunType = "pull",
                        UserId = null,
                        MeasurementCount = measurements.Count,
                        Status = "success"
                    };
                    _db.SyncRuns.Add(syncRun);
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx,
                        "SyncMeasurementsController: failed to record pull SyncRun — measurements were returned.");
                }

                return Ok(new MeasurementPullResponse
                {
                    Measurements = measurements,
                    Total = measurements.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncMeasurementsController: [{SyncRunId}] pull failed.", syncRunId);
                try
                {
                    var failedRun = new Sync.Domain.Entities.SyncRun
                    {
                        Id = syncRunId,
                        OccurredAt = DateTime.UtcNow,
                        RunType = "pull",
                        UserId = null,
                        MeasurementCount = 0,
                        Status = "failed",
                        ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message
                    };
                    _db.SyncRuns.Add(failedRun);
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                catch { /* best-effort */ }
                return StatusCode(500, new { message = "Pull failed.", error = ex.Message });
            }
        }
    }
}
