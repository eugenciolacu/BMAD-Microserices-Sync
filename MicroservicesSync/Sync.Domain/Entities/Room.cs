namespace Sync.Domain.Entities;

/// <summary>
/// Represents a room within a building in the sync experiment reference data.
/// </summary>
public class Room
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid BuildingId { get; set; }

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public Building Building { get; set; } = null!;
    public ICollection<Surface> Surfaces { get; set; } = new List<Surface>();
}
