using Microsoft.EntityFrameworkCore;
using Sync.Domain.Entities;

namespace Sync.Infrastructure.Data;

/// <summary>
/// Seeds ServerService SQL Server database with baseline reference data.
/// Idempotent: data is only inserted when tables are empty; safe to call on every startup.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(ServerDbContext db)
    {
        // Idempotent guard — do nothing if data already exists.
        if (await db.Users.AnyAsync()) return;

        // Explicit transaction: ensures all-or-nothing insert even if SaveChangesAsync
        // is split in the future, or if the implicit transaction is disabled.
        await using var transaction = await db.Database.BeginTransactionAsync();

        // ─── Users (GUIDs must match ClientIdentity__UserId in docker-compose.yml) ───────
        var users = new[]
        {
            new User { Id = new Guid("00000000-0000-0000-0000-000000000001"), Username = "user1", Email = "user1@experiment.local" },
            new User { Id = new Guid("00000000-0000-0000-0000-000000000002"), Username = "user2", Email = "user2@experiment.local" },
            new User { Id = new Guid("00000000-0000-0000-0000-000000000003"), Username = "user3", Email = "user3@experiment.local" },
            new User { Id = new Guid("00000000-0000-0000-0000-000000000004"), Username = "user4", Email = "user4@experiment.local" },
            new User { Id = new Guid("00000000-0000-0000-0000-000000000005"), Username = "user5", Email = "user5@experiment.local" },
        };

        // ─── Buildings ───────────────────────────────────────────────────────────────────
        var b1Id = new Guid("10000000-0000-0000-0000-000000000001");
        var b2Id = new Guid("10000000-0000-0000-0000-000000000002");

        var buildings = new[]
        {
            new Building { Id = b1Id, Identifier = "building-alpha" },
            new Building { Id = b2Id, Identifier = "building-beta"  },
        };

        // ─── Rooms (2 per building) ───────────────────────────────────────────────────────
        var r1Id = new Guid("20000000-0000-0000-0000-000000000001");
        var r2Id = new Guid("20000000-0000-0000-0000-000000000002");
        var r3Id = new Guid("20000000-0000-0000-0000-000000000003");
        var r4Id = new Guid("20000000-0000-0000-0000-000000000004");

        var rooms = new[]
        {
            new Room { Id = r1Id, Identifier = "room-a1", BuildingId = b1Id },
            new Room { Id = r2Id, Identifier = "room-a2", BuildingId = b1Id },
            new Room { Id = r3Id, Identifier = "room-b1", BuildingId = b2Id },
            new Room { Id = r4Id, Identifier = "room-b2", BuildingId = b2Id },
        };

        // ─── Surfaces (2 per room) ────────────────────────────────────────────────────────
        var surfaces = new[]
        {
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000001"), Identifier = "surface-a1-s1", RoomId = r1Id },
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000002"), Identifier = "surface-a1-s2", RoomId = r1Id },
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000003"), Identifier = "surface-a2-s1", RoomId = r2Id },
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000004"), Identifier = "surface-a2-s2", RoomId = r2Id },
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000005"), Identifier = "surface-b1-s1", RoomId = r3Id },
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000006"), Identifier = "surface-b1-s2", RoomId = r3Id },
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000007"), Identifier = "surface-b2-s1", RoomId = r4Id },
            new Surface { Id = new Guid("30000000-0000-0000-0000-000000000008"), Identifier = "surface-b2-s2", RoomId = r4Id },
        };

        // ─── Cells (2 per surface) ────────────────────────────────────────────────────────
        var cells = new[]
        {
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000001"), Identifier = "cell-a1s1-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000001") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000002"), Identifier = "cell-a1s1-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000001") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000003"), Identifier = "cell-a1s2-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000002") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000004"), Identifier = "cell-a1s2-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000002") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000005"), Identifier = "cell-a2s1-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000003") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000006"), Identifier = "cell-a2s1-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000003") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000007"), Identifier = "cell-a2s2-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000004") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000008"), Identifier = "cell-a2s2-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000004") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000009"), Identifier = "cell-b1s1-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000005") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000010"), Identifier = "cell-b1s1-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000005") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000011"), Identifier = "cell-b1s2-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000006") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000012"), Identifier = "cell-b1s2-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000006") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000013"), Identifier = "cell-b2s1-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000007") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000014"), Identifier = "cell-b2s1-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000007") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000015"), Identifier = "cell-b2s2-c1", SurfaceId = new Guid("30000000-0000-0000-0000-000000000008") },
            new Cell { Id = new Guid("40000000-0000-0000-0000-000000000016"), Identifier = "cell-b2s2-c2", SurfaceId = new Guid("30000000-0000-0000-0000-000000000008") },
        };

        await db.Users.AddRangeAsync(users);
        await db.Buildings.AddRangeAsync(buildings);
        await db.Rooms.AddRangeAsync(rooms);
        await db.Surfaces.AddRangeAsync(surfaces);
        await db.Cells.AddRangeAsync(cells);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
