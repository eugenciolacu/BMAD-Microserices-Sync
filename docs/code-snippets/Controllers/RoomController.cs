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
    public class RoomController : ControllerBase
    {
        private readonly IGenericService<Room, RoomDto, RoomCreateDto, RoomUpdateDto> _roomService;

        public RoomController(IGenericService<Room, RoomDto, RoomCreateDto, RoomUpdateDto> roomService)
        {
            _roomService = roomService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var room = await _roomService.GetByIdAsync(id);
            if (room == null)
                return NotFound();
            
            return Ok(room);
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

            var result = await _roomService.GetPagedAsync(page, pageSize, sortBy, sortOrder, gridFilter);
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
        public async Task<IActionResult> Create([FromBody] RoomCreateDto roomCreateDto)
        {
            var created = await _roomService.CreateAsync(roomCreateDto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] RoomUpdateDto roomUpdateDto)
        {
            var updated = await _roomService.UpdateAsync(id, roomUpdateDto);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _roomService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}