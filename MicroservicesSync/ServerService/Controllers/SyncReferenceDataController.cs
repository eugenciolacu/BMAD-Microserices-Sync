using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerService.Models.Sync;
using Sync.Infrastructure.Data;

namespace ServerService.Controllers;

[ApiController]
[Route("api/v1/sync")]
public class SyncReferenceDataController : ControllerBase
{
    private readonly ServerDbContext _db;

    public SyncReferenceDataController(ServerDbContext db) => _db = db;

    /// <summary>
    /// Returns all reference data (Users, Buildings, Rooms, Surfaces, Cells).
    /// Used by ClientService on first startup to populate its local SQLite database.
    /// No authentication required (per architecture ADR-002).
    /// </summary>
    [HttpGet("reference-data")]
    public async Task<ActionResult<ReferenceDataDto>> GetReferenceData()
    {
        var dto = new ReferenceDataDto
        {
            Users = await _db.Users.AsNoTracking()
                .Select(u => new UserDto { Id = u.Id, Username = u.Username, Email = u.Email })
                .ToListAsync(),

            Buildings = await _db.Buildings.AsNoTracking()
                .Select(b => new BuildingDto { Id = b.Id, Identifier = b.Identifier })
                .ToListAsync(),

            Rooms = await _db.Rooms.AsNoTracking()
                .Select(r => new RoomDto { Id = r.Id, Identifier = r.Identifier, BuildingId = r.BuildingId })
                .ToListAsync(),

            Surfaces = await _db.Surfaces.AsNoTracking()
                .Select(s => new SurfaceDto { Id = s.Id, Identifier = s.Identifier, RoomId = s.RoomId })
                .ToListAsync(),

            Cells = await _db.Cells.AsNoTracking()
                .Select(c => new CellDto { Id = c.Id, Identifier = c.Identifier, SurfaceId = c.SurfaceId })
                .ToListAsync(),
        };

        return Ok(dto);
    }
}
