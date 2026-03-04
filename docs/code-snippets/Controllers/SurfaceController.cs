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
    public class SurfaceController : ControllerBase
    {
        private readonly IGenericService<Surface, SurfaceDto, SurfaceCreateDto, SurfaceUpdateDto> _surfaceService;

        public SurfaceController(IGenericService<Surface, SurfaceDto, SurfaceCreateDto, SurfaceUpdateDto> surfaceService)
        {
            _surfaceService = surfaceService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var surface = await _surfaceService.GetByIdAsync(id);
            if (surface == null)
                return NotFound();
            
            return Ok(surface);
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
                gridFilter = JsonSerializer.Deserialize<JqGridFilter>(filters, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }

            var result = await _surfaceService.GetPagedAsync(page, pageSize, sortBy, sortOrder, gridFilter);
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
        public async Task<IActionResult> Create([FromBody] SurfaceCreateDto surfaceCreateDto)
        {
            var created = await _surfaceService.CreateAsync(surfaceCreateDto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] SurfaceUpdateDto surfaceUpdateDto)
        {
            var updated = await _surfaceService.UpdateAsync(id, surfaceUpdateDto);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _surfaceService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}