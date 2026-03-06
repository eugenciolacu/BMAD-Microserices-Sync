namespace ClientService.Models.Sync;

/// <summary>
/// Mirrors the POST /api/v1/sync/measurements/push request body for ServerService.
/// Used only by MeasurementSyncService to push local measurements upstream.
/// </summary>
internal sealed class ClientMeasurementPushRequest
{
    public List<ClientMeasurementPushItemDto> Measurements { get; set; } = new();
}

internal sealed class ClientMeasurementPushItemDto
{
    public Guid Id { get; set; }
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid UserId { get; set; }
    public Guid CellId { get; set; }
}
