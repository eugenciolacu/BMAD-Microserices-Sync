namespace Sync.Domain.Entities;

/// <summary>
/// Represents a building in the sync experiment reference data.
/// </summary>
public class Building
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
