namespace ServerService.Models
{
    public class Cell
    {
        public int Id { get; set; }
        public string Identifier { get; set; } = null!;
        public int SurfaceId { get; set; }
        public Surface Surface { get; set; } = null!;
        public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    }
}