namespace Sync.Domain.Entities;

/// <summary>
/// Represents a user identity in the sync experiment.
/// UserId GUIDs must match ClientIdentity__UserId values in docker-compose.yml.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Concurrency token — mapped as rowversion on SQL Server, ignored/replaced on SQLite.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}
