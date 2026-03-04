using Microsoft.AspNetCore.Mvc;
using ServerService.DTOs;
using ServerService.Models;
using ServerService.Models.Grid;
using ServerService.Services;
using System.Text.Json;

namespace ServerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeasurementController : ControllerBase
    {
        private readonly IGenericService<Measurement, MeasurementDto, MeasurementCreateDto, MeasurementUpdateDto> _measurementService;

        public MeasurementController(IGenericService<Measurement, MeasurementDto, MeasurementCreateDto, MeasurementUpdateDto> measurementService)
        {
            _measurementService = measurementService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var measurement = await _measurementService.GetByIdAsync(id);
            if (measurement == null)
                return NotFound();
            
            return Ok(measurement);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? filters = null)
        {
            JqGridFilter? gridFilter = null;
            
            if (!string.IsNullOrWhiteSpace(filters))
            {
                try
                {
                    gridFilter = JsonSerializer.Deserialize<JqGridFilter>(filters, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                }
                catch (JsonException ex)
                {
                    return BadRequest($"Invalid filter format: {ex.Message}");
                }
            }

            var result = await _measurementService.GetPagedAsync(page, pageSize, sortBy, sortOrder, gridFilter);
            var totalPages = (int)Math.Ceiling((double)result.Total / pageSize);
            
            return Ok(new 
            { 
                Data = result.Data, 
                TotalCount = result.Total,
                TotalPages = totalPages,
                Page = result.Page, 
                PageSize = result.PageSize 
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MeasurementCreateDto measurementCreateDto)
        {
            var created = await _measurementService.CreateAsync(measurementCreateDto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] MeasurementUpdateDto measurementUpdateDto)
        {
            var updated = await _measurementService.UpdateAsync(id, measurementUpdateDto);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _measurementService.DeleteAsync(id);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}
