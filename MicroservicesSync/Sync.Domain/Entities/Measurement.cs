namespace Sync.Domain.Entities;

/// <summary>
/// Represents a measurement recorded by a client during a sync experiment run.
/// Measurements are client-writable; reference data (Users, Buildings, Rooms, Surfaces, Cells) is read-only on clients.
/// </summary>
public class Measurement
{
    public Guid Id { get; set; }
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime? SyncedAt { get; set; }
    public Guid UserId { get; set; }
    public Guid CellId { get; set; }

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public User User { get; set; } = null!;
    public Cell Cell { get; set; } = null!;
}
