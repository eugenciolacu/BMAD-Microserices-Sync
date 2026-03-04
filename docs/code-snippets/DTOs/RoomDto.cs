namespace ServerService.DTOs
{
    public class RoomDto
    {
        public int Id { get; set; }
        public string Identifier { get; set; } = null!;
        public int BuildingId { get; set; }
    }
}