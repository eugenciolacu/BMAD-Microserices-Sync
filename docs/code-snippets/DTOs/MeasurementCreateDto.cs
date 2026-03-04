namespace ServerService.DTOs
{
    public class MeasurementCreateDto
    {
        public float Alpha { get; set; }
        public float Beta { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public int CellId { get; set; }
        public int UserId { get; set; }
    }
}
