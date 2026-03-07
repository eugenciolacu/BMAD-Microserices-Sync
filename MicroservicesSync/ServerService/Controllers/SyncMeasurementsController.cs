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
                "SyncMeasurementsController: pushed {Count} measurements in {Batches} batch(es).",
                request.Measurements.Count, batches.Count);

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
                "SyncMeasurementsController: push failed — transaction rolled back.");
            return StatusCode(500, new { message = "Push failed. Transaction rolled back.", error = ex.Message });
        }
    }

    [HttpGet("measurements/pull")]
    public async Task<IActionResult> Pull(CancellationToken cancellationToken)
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
            "SyncMeasurementsController: pull requested — returning {Count} measurements.",
            measurements.Count);

        return Ok(new MeasurementPullResponse
        {
            Measurements = measurements,
            Total = measurements.Count
        });
    }
}
