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
    public class UserController : ControllerBase
    {
        private readonly IGenericService<User, UserDto, UserCreateDto, UserUpdateDto> _userService;

        public UserController(IGenericService<User, UserDto, UserCreateDto, UserUpdateDto> userService)
        {
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound();
            
            return Ok(user);
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

            var result = await _userService.GetPagedAsync(page, pageSize, sortBy, sortOrder, gridFilter);
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
        public async Task<IActionResult> Create([FromBody] UserCreateDto userCreateDto)
        {
            var created = await _userService.CreateAsync(userCreateDto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UserUpdateDto userUpdateDto)
        {
            var updated = await _userService.UpdateAsync(id, userUpdateDto);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _userService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
