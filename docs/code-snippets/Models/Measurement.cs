namespace ServerService.Models
{
    public class Measurement
    {
        public int Id { get; set; }
        public float Alpha { get; set; }
        public float Beta { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public int CellId { get; set; }
        public Cell Cell { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}