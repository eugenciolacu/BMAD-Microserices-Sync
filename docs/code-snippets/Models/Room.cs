namespace ServerService.Models
{
    public class Room
    {
        public int Id { get; set; }
        public string Identifier { get; set; } = null!;
        public int BuildingId { get; set; }
        public Building Building { get; set; } = null!;
        public ICollection<Surface> Surfaces { get; set; } = new List<Surface>();
    }
}
