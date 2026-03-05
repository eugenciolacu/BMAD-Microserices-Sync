namespace Sync.Domain.Entities;

/// <summary>
/// Represents a surface within a room in the sync experiment reference data.
/// </summary>
public class Surface
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid RoomId { get; set; }

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public Room Room { get; set; } = null!;
    public ICollection<Cell> Cells { get; set; } = new List<Cell>();
}
