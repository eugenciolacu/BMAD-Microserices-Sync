using Microsoft.EntityFrameworkCore;

namespace Sync.Infrastructure.Data;

/// <summary>
/// Resets ServerService and ClientService databases to clean baseline state.
/// Uses EF Core ExecuteDeleteAsync for bulk deletes (no entity loading).
/// FK-safe deletion order is enforced: Measurements → Cells → Surfaces → Rooms → Buildings → Users.
/// </summary>
public static class DatabaseResetter
{
    /// <summary>
    /// Clears all data from ServerService SQL Server database and re-seeds reference data.
    /// After completion: 5 Users, 2 Buildings, 4 Rooms, 8 Surfaces, 16 Cells, 0 Measurements.
    /// </summary>
    public static async Task ResetServerAsync(ServerDbContext db, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        // FK-safe bulk deletes (no entity loading — O(1) SQL DELETE FROM statements)
        await db.Measurements.ExecuteDeleteAsync(cancellationToken);
        await db.Cells.ExecuteDeleteAsync(cancellationToken);
        await db.Surfaces.ExecuteDeleteAsync(cancellationToken);
        await db.Rooms.ExecuteDeleteAsync(cancellationToken);
        await db.Buildings.ExecuteDeleteAsync(cancellationToken);
        await db.Users.ExecuteDeleteAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        // Clear the EF Core change tracker so SeedAsync can AddRangeAsync the same stable GUIDs
        // without a duplicate-tracking conflict. ExecuteDeleteAsync operates at the DB level and
        // does NOT remove previously-tracked entities from the in-memory identity map.
        db.ChangeTracker.Clear();

        // NOTE: SeedAsync opens a separate internal transaction. There is a brief window between
        // CommitAsync above and the SeedAsync transaction start where the DB has zero reference data.
        // Acceptable for a dev-tool reset endpoint. DatabaseSeeder.SeedAsync does not accept a
        // CancellationToken — cancellation after CommitAsync will leave the DB empty until SeedAsync
        // completes on the next call.
        await DatabaseSeeder.SeedAsync(db);
    }

    /// <summary>
    /// Clears ALL data from a ClientService SQLite database (reference data + measurements).
    /// After completion: all tables empty. Reference data is repopulated via startup auto-pull
    /// or POST /api/v1/admin/pull-reference-data.
    /// </summary>
    public static async Task ResetClientAsync(ClientDbContext db, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        // FK-safe bulk deletes (no entity loading — O(1) SQL DELETE FROM statements)
        await db.Measurements.ExecuteDeleteAsync(cancellationToken);
        await db.Cells.ExecuteDeleteAsync(cancellationToken);
        await db.Surfaces.ExecuteDeleteAsync(cancellationToken);
        await db.Rooms.ExecuteDeleteAsync(cancellationToken);
        await db.Buildings.ExecuteDeleteAsync(cancellationToken);
        await db.Users.ExecuteDeleteAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        // Clear the EF Core change tracker for defensive consistency — prevents stale identity-map
        // entries from causing duplicate-tracking exceptions in any follow-up operation on this context.
        db.ChangeTracker.Clear();
        // Do NOT re-seed ClientService — startup auto-pull logic (or pull-reference-data endpoint)
        // repopulates reference data when local DB is empty.
    }
}
