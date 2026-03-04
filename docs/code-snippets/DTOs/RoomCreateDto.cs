namespace ServerService.DTOs
{
    public class RoomCreateDto
    {
        public string Identifier { get; set; } = null!;
        public int BuildingId { get; set; }
    }
}