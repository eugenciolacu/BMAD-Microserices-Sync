namespace Sync.Domain.Entities;

/// <summary>
/// Represents a measurement cell on a surface in the sync experiment reference data.
/// </summary>
public class Cell
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid SurfaceId { get; set; }

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public Surface Surface { get; set; } = null!;
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}
