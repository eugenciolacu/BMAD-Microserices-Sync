using ClientService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sync.Infrastructure.Data;

namespace ClientService.Controllers;

[ApiController]
[Route("api/v1/measurements")]
public class MeasurementsController : ControllerBase
{
    private readonly ClientDbContext _db;
    private readonly MeasurementGenerationService _generationService;
    private readonly MeasurementSyncService _syncService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MeasurementsController> _logger;

    public MeasurementsController(
        ClientDbContext db,
        MeasurementGenerationService generationService,
        MeasurementSyncService syncService,
        IHttpClientFactory httpClientFactory,
        ILogger<MeasurementsController> logger)
    {
        _db = db;
        _generationService = generationService;
        _syncService = syncService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count(CancellationToken cancellationToken)
    {
        var count = await _db.Measurements
            .AsNoTracking()
            .CountAsync(cancellationToken);
        _logger.LogInformation(
            "MeasurementsController: count requested — {Count} measurements.", count);
        return Ok(new { count });
    }

    [HttpGet("server-count")]
    public async Task<IActionResult> ServerCount(CancellationToken cancellationToken)
    {
        try
        {
            var http = _httpClientFactory.CreateClient("ServerService");
            using var response = await http.GetAsync("api/v1/sync/measurements/count", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { message = "Failed to reach ServerService count endpoint." });
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: server-count proxy call failed.");
            return StatusCode(500, new { message = "Failed to reach ServerService.", error = ex.Message });
        }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _generationService.GenerateMeasurementsAsync(cancellationToken);
            return Ok(new { message = $"Generated {count} measurements.", count });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ClientService: measurement generation failed — prerequisite not met.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: measurement generation failed.");
            return StatusCode(500, new { message = "Generation failed.", error = ex.Message });
        }
    }

    [HttpPost("push")]
    public async Task<IActionResult> Push(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _syncService.PushAsync(cancellationToken);
            _logger.LogInformation("ClientService: push completed — {Count} measurements.", result.Count);
            return Ok(new { message = result.Message, pushed = result.Count });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ClientService: push failed — server rejected.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: push failed unexpectedly.");
            return StatusCode(500, new { message = "Push failed.", error = ex.Message });
        }
    }

    [HttpPost("pull")]
    public async Task<IActionResult> Pull(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _syncService.PullAsync(cancellationToken);
            _logger.LogInformation("ClientService: pull completed — {Count} new measurements.", result.Count);
            return Ok(new { message = result.Message, pulled = result.Count });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ClientService: pull failed — operation error.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClientService: pull failed unexpectedly.");
            return StatusCode(500, new { message = "Pull failed.", error = ex.Message });
        }
    }
}
