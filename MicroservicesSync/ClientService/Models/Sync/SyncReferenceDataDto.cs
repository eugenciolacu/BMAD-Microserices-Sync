namespace ClientService.Models.Sync;

/// <summary>
/// Mirrors the GET /api/v1/sync/reference-data response shape from ServerService.
/// Used only during ClientService startup to pull and persist reference data locally.
/// </summary>
internal sealed class SyncReferenceDataDto
{
    public List<SyncUserDto> Users { get; set; } = new();
    public List<SyncBuildingDto> Buildings { get; set; } = new();
    public List<SyncRoomDto> Rooms { get; set; } = new();
    public List<SyncSurfaceDto> Surfaces { get; set; } = new();
    public List<SyncCellDto> Cells { get; set; } = new();
}

internal sealed class SyncUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

internal sealed class SyncBuildingDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
}

internal sealed class SyncRoomDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid BuildingId { get; set; }
}

internal sealed class SyncSurfaceDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
}

internal sealed class SyncCellDto
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public Guid SurfaceId { get; set; }
}
