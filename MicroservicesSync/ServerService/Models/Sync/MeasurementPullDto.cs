namespace ServerService.Models.Sync;

public class MeasurementPullItemDto
{
    public Guid Id { get; set; }
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid UserId { get; set; }
    public Guid CellId { get; set; }
}

public class MeasurementPullResponse
{
    public List<MeasurementPullItemDto> Measurements { get; set; } = new();
    public int Total { get; set; }
}
