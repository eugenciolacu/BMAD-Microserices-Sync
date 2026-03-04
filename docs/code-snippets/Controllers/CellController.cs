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
    public class CellController : ControllerBase
    {
        private readonly IGenericService<Cell, CellDto, CellCreateDto, CellUpdateDto> _cellService;

        public CellController(IGenericService<Cell, CellDto, CellCreateDto, CellUpdateDto> cellService)
        {
            _cellService = cellService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cell = await _cellService.GetByIdAsync(id);
            if (cell == null)
                return NotFound();
            
            return Ok(cell);
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

            var result = await _cellService.GetPagedAsync(page, pageSize, sortBy, sortOrder, gridFilter);
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
        public async Task<IActionResult> Create([FromBody] CellCreateDto cellCreateDto)
        {
            var created = await _cellService.CreateAsync(cellCreateDto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] CellUpdateDto cellUpdateDto)
        {
            var updated = await _cellService.UpdateAsync(id, cellUpdateDto);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _cellService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
