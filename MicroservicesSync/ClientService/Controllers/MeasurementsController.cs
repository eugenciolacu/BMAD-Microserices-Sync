using ClientService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientService.Controllers;

[ApiController]
[Route("api/v1/measurements")]
public class MeasurementsController : ControllerBase
{
    private readonly MeasurementGenerationService _generationService;
    private readonly ILogger<MeasurementsController> _logger;

    public MeasurementsController(
        MeasurementGenerationService generationService,
        ILogger<MeasurementsController> logger)
    {
        _generationService = generationService;
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
}
