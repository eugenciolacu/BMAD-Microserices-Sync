namespace ServerService.Models.Sync;

public class ReferenceDataDto
{
    public List<UserDto> Users { get; set; } = new();
    public List<BuildingDto> Buildings { get; set; } = new();
    public List<RoomDto> Rooms { get; set; } = new();
    public List<SurfaceDto> Surfaces { get; set; } = new();
    public List<CellDto> Cells { get; set; } = new();
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class BuildingDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
}

public class RoomDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid BuildingId { get; set; }
}

public class SurfaceDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
}

public class CellDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid SurfaceId { get; set; }
}
