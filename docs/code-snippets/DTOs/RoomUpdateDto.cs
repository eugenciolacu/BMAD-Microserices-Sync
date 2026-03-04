namespace ServerService.DTOs
{
    public class RoomUpdateDto
    {
        public string Identifier { get; set; } = null!;
        public int BuildingId { get; set; }
    }
}