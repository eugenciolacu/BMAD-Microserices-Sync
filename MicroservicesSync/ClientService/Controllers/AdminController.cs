using ClientService.Models.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using System.Net.Http.Json;

namespace ClientService.Controllers;

/// <summary>
/// Administrative actions for ClientService: reset database to clean baseline
/// and manually trigger reference-data pull from ServerService.
/// Story 1.5 — AC#2.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly ClientDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ClientDbContext db, IHttpClientFactory httpClientFactory, ILogger<AdminController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // NOTE (M4): AdminController injects ClientDbContext directly rather than via an Application-layer
    // service. This is a deliberate, acknowledged deviation from the project-context layering rule
    // (Web → Application → Domain only) for this thin admin/dev-tool endpoint.
    // Future hardening: extract to a ResetService in Sync.Application if admin scope grows.

    /// <summary>
    /// Resets the ClientService SQLite database to empty state.
    /// Deletes all data (reference data + measurements) in FK-safe order.
    /// After reset, use pull-reference-data or restart the container to repopulate reference data.
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("ClientService: reset initiated.");
            await DatabaseResetter.ResetClientAsync(_db, cancellationToken);
            _logger.LogInformation("ClientService: reset complete. DB cleared — reference data will be pulled on next startup trigger.");
            return Ok(new { message = "ClientService reset complete. Restart or trigger reference pull to reload reference data." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: reset failed.");
            return StatusCode(500, new { message = "Reset failed.", error = ex.Message });
        }
    }

    /// <summary>
    /// Manually triggers a reference-data pull from ServerService.
    /// Returns 400 if reference data already exists (idempotency guard).
    /// Idempotent: only loads data if the local DB is fully seeded (Cells present).
    /// Uses Cells (leaf entity) as completeness guard — tolerates partial-failure recovery.
    /// </summary>
    [HttpPost("pull-reference-data")]
    public async Task<IActionResult> PullReferenceData(CancellationToken cancellationToken)
    {
        // Idempotency guard: check Cells (leaf entity) — absent after partial-failure mid-seed,
        // allowing re-pull recovery without a manual reset in those edge cases.
        if (await _db.Cells.AnyAsync(cancellationToken))
        {
            return BadRequest(new { message = "Reference data already present." });
        }

        try
        {
            _logger.LogInformation("ClientService: pulling reference data from ServerService.");

            // TODO: extract to ReferenceDataLoader to avoid duplication with Program.cs startup logic.
            var http = _httpClientFactory.CreateClient("ServerService");
            var refData = await http.GetFromJsonAsync<SyncReferenceDataDto>("api/v1/sync/reference-data", cancellationToken);

            if (refData is null)
            {
                _logger.LogWarning("ClientService: ServerService returned null reference data.");
                return StatusCode(502, new { message = "ServerService returned empty reference data." });
            }

            await _db.Users.AddRangeAsync(
                refData.Users.Select(u => new User { Id = u.Id, Username = u.Username, Email = u.Email }));

            await _db.Buildings.AddRangeAsync(
                refData.Buildings.Select(b => new Building { Id = b.Id, Identifier = b.Identifier }));

            await _db.Rooms.AddRangeAsync(
                refData.Rooms.Select(r => new Room { Id = r.Id, Identifier = r.Identifier, BuildingId = r.BuildingId }));

            await _db.Surfaces.AddRangeAsync(
                refData.Surfaces.Select(s => new Surface { Id = s.Id, Identifier = s.Identifier, RoomId = s.RoomId }));

            await _db.Cells.AddRangeAsync(
                refData.Cells.Select(c => new Cell { Id = c.Id, Identifier = c.Identifier, SurfaceId = c.SurfaceId }));

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ClientService: reference data loaded successfully.");
            return Ok(new { message = "Reference data loaded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: failed to pull reference data from ServerService.");
            return StatusCode(500, new { message = "Failed to pull reference data.", error = ex.Message });
        }
    }
}
