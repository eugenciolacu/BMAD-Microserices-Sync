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
    public class BuildingController : ControllerBase
    {
        private readonly IGenericService<Building, BuildingDto, BuildingCreateDto, BuildingUpdateDto> _buildingService;

        public BuildingController(IGenericService<Building, BuildingDto, BuildingCreateDto, BuildingUpdateDto> buildingService)
        {
            _buildingService = buildingService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var building = await _buildingService.GetByIdAsync(id);
            if (building == null)
                return NotFound();
            
            return Ok(building);
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

            var result = await _buildingService.GetPagedAsync(page, pageSize, sortBy, sortOrder, gridFilter);
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
        public async Task<IActionResult> Create([FromBody] BuildingCreateDto buildingCreateDto)
        {
            var created = await _buildingService.CreateAsync(buildingCreateDto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] BuildingUpdateDto buildingUpdateDto)
        {
            var updated = await _buildingService.UpdateAsync(id, buildingUpdateDto);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _buildingService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}