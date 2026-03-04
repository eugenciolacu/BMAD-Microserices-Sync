namespace ServerService.Models
{
    public class Surface
    {
        public int Id { get; set; }
        public string Identifier { get; set; } = null!;
        public int RoomId { get; set; }
        public Room Room { get; set; } = null!;
        public ICollection<Cell> Cells { get; set; } = new List<Cell>();
    }
}