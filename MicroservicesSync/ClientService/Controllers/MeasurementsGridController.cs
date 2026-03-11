using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using Sync.Infrastructure.Grid;
using System.Text.Json;

namespace ClientService.Controllers;

/// <summary>
/// jqGrid-compatible paged/filtered/sorted API for Measurement inspection on ClientService.
/// Story 3.2 — AC#2, AC#3.
/// Full CRUD for Measurements (ClientService has write access to Measurements).
/// </summary>
[ApiController]
[Route("api/v1/measurements-grid")]
public class MeasurementsGridController : ControllerBase
{
    private readonly ClientDbContext _db;

    public MeasurementsGridController(ClientDbContext db)
    {
        _db = db;
    }

    // NOTE (M3.2): MeasurementsGridController injects ClientDbContext directly.
    // Consistent with the acknowledged deviation in AdminController (NOTE M4)
    // and MeasurementsController. Acceptable for this experiment scope.

    /// <summary>
    /// Returns paged, sorted, filtered measurements for jqGrid consumption.
    /// </summary>
    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] string? filters = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        IQueryable<Measurement> query = _db.Measurements.AsNoTracking();

        // Apply jqGrid filters
        if (!string.IsNullOrWhiteSpace(filters))
        {
            try
            {
                var gridFilter = JsonSerializer.Deserialize<JqGridFilter>(filters,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (gridFilter != null)
                    query = JqGridHelper.ApplyFilters(query, gridFilter);
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Invalid filter format." });
            }
        }

        // Total count (after filter, before paging)
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // Apply sorting
        query = JqGridHelper.ApplySort(query, sortBy, sortOrder);

        // Apply paging
        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.Value,
                m.RecordedAt,
                m.SyncedAt,
                m.UserId,
                m.CellId
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data,
            totalCount,
            totalPages,
            page,
            pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var measurement = await _db.Measurements
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id,
                m.Value,
                m.RecordedAt,
                m.SyncedAt,
                m.UserId,
                m.CellId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (measurement == null)
            return NotFound();

        return Ok(measurement);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] MeasurementCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = new Measurement
        {
            Id = Guid.NewGuid(),
            Value = request.Value,
            RecordedAt = request.RecordedAt,
            SyncedAt = null,
            UserId = request.UserId,
            CellId = request.CellId
        };

        _db.Measurements.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] MeasurementUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Measurements.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();

        entity.Value = request.Value;
        entity.RecordedAt = request.RecordedAt;
        entity.UserId = request.UserId;
        entity.CellId = request.CellId;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { entity.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _db.Measurements.FindAsync([id], cancellationToken);
        if (entity == null) return NotFound();

        _db.Measurements.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

// Request DTOs — nested in same file. Identical shape to ServerService's version.
// Kept separate (not shared via Sync.Domain/Sync.Infrastructure) because these are
// web-layer concerns and the two services ship independently.

public class MeasurementCreateRequest
{
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid UserId { get; set; }
    public Guid CellId { get; set; }
}

public class MeasurementUpdateRequest
{
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid UserId { get; set; }
    public Guid CellId { get; set; }
}
