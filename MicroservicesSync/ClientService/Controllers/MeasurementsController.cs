using ClientService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientService.Controllers;

[ApiController]
[Route("api/v1/measurements")]
public class MeasurementsController : ControllerBase
{
    private readonly MeasurementGenerationService _generationService;
    private readonly MeasurementSyncService _syncService;
    private readonly ILogger<MeasurementsController> _logger;

    public MeasurementsController(
        MeasurementGenerationService generationService,
        MeasurementSyncService syncService,
        ILogger<MeasurementsController> logger)
    {
        _generationService = generationService;
        _syncService = syncService;
        _logger = logger;
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
