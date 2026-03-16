namespace Sync.Domain.Entities;

/// <summary>Represents a record of a single sync push or pull operation processed by ServerService.</summary>
public class SyncRun
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }      // UTC timestamp of the run
    public string RunType { get; set; } = string.Empty;  // "push" | "pull"
    public Guid? UserId { get; set; }               // FK → Users.Id; null for server-side pull operations
    public int MeasurementCount { get; set; }      // number of records processed
    public string Status { get; set; } = string.Empty;   // "success" | "failed"
    public string? ErrorMessage { get; set; }      // null on success; set on failure

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation (optional; null for pull runs which have no single user identity)
    public User? User { get; set; }
}
