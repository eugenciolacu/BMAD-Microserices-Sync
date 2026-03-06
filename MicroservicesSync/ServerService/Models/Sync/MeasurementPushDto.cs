namespace ServerService.Models.Sync;

public class MeasurementPushRequest
{
    public List<MeasurementPushItemDto> Measurements { get; set; } = new();
}

public class MeasurementPushItemDto
{
    public Guid Id { get; set; }
    public decimal Value { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid UserId { get; set; }
    public Guid CellId { get; set; }
}

public class MeasurementPushResponse
{
    public int Pushed { get; set; }
    public string Message { get; set; } = string.Empty;
}
