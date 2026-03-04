namespace ServerService.Models
{
    public class Building
    {
        public int Id { get; set; }
        public string Identifier { get; set; } = null!;
        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}