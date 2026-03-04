namespace ServerService.DTOs
{
    public class SurfaceCreateDto
    {
        public string Identifier { get; set; } = null!;
        public int RoomId { get; set; }
    }
}