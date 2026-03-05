using Microsoft.AspNetCore.Mvc;
using Sync.Infrastructure.Data;

namespace ServerService.Controllers;

/// <summary>
/// Administrative actions for ServerService: reset database to clean baseline.
/// Story 1.5 — AC#1.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly ServerDbContext _db;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ServerDbContext db, ILogger<AdminController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // NOTE (M4): AdminController injects ServerDbContext directly rather than via an Application-layer
    // service. This is a deliberate, acknowledged deviation from the project-context layering rule
    // (Web → Application → Domain only) for this thin admin/dev-tool endpoint.
    // Future hardening: extract to a ResetService in Sync.Application if admin scope grows.

    /// <summary>
    /// Resets the ServerService SQL Server database to clean baseline state.
    /// Deletes all data (FK-safe order) then re-seeds reference data via DatabaseSeeder.
    /// Result: 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells, 0 Measurements.
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("ServerService: reset initiated.");
            await DatabaseResetter.ResetServerAsync(_db, cancellationToken);
            _logger.LogInformation("ServerService: reset complete. Reference data re-seeded.");
            return Ok(new { message = "ServerService reset complete." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServerService: reset failed.");
            return StatusCode(500, new { message = "Reset failed.", error = ex.Message });
        }
    }
}
