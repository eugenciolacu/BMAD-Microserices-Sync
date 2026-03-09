using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sync.Infrastructure.Data;

namespace ServerService.Controllers;

[ApiController]
[Route("api/v1/sync-runs")]
public class SyncRunsController : ControllerBase
{
    private readonly ServerDbContext _db;
    private readonly ILogger<SyncRunsController> _logger;

    public SyncRunsController(ServerDbContext db, ILogger<SyncRunsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // NOTE (M3.1): SyncRunsController injects ServerDbContext directly.
    // Consistent with the acknowledged deviation in AdminController (NOTE M4) and
    // SyncMeasurementsController. Acceptable for this experiment scope.

    /// <summary>
    /// Returns recent sync runs, newest first. Optional query params: userId, runType (push|pull), limit (default 50, max 200).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecent(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? runType = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > 200) limit = 50;

        // Whitelist runType to prevent unexpected filter values
        if (runType is not null && runType != "push" && runType != "pull")
            return BadRequest(new { message = "runType must be 'push' or 'pull'." });

        var query = _db.SyncRuns
            .AsNoTracking()
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        if (runType is not null)
            query = query.Where(r => r.RunType == runType);

        var runs = await query
            .OrderByDescending(r => r.OccurredAt)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                r.OccurredAt,
                r.RunType,
                r.UserId,
                Username = r.User != null ? r.User.Username : "(unknown)",
                r.MeasurementCount,
                r.Status,
                r.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "SyncRunsController: returned {Count} sync run(s).", runs.Count);

        return Ok(new { total = runs.Count, runs });
    }
}
