namespace ClientService.Models.Sync;

/// <summary>
/// Mirrors the GET /api/v1/sync/measurements/pull response body from ServerService.
/// Used only by MeasurementSyncService to receive and apply consolidated measurements.
/// </summary>
internal sealed class ClientMeasurementPullResponse
{
    public List<ClientMeasurementPullItemDto> Measurements { get; set; } = new();
    public int Total { get; set; }
}

internal sealed class ClientMeasurementPullItemDto
{
    public Guid Id { get; set; }
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid UserId { get; set; }
    public Guid CellId { get; set; }
}
