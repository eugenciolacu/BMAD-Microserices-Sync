using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ServerService.Controllers;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using Xunit;

namespace MicroservicesSync.Tests.SyncRuns;

/// <summary>
/// SQLite-compatible subclass of ServerDbContext for SyncRun unit tests.
/// Overrides RowVersion columns to ValueGeneratedNever so SQLite accepts
/// Array.Empty&lt;byte&gt;() as the default value (same pattern as TestableServerDbContextForPull).
/// </summary>
internal sealed class TestableServerDbContextForSyncRuns : ServerDbContext
{
    public TestableServerDbContextForSyncRuns(DbContextOptions<ServerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entityType.FindProperty("RowVersion");
            if (rowVersionProp != null)
                modelBuilder.Entity(entityType.ClrType)
                    .Property("RowVersion")
                    .ValueGeneratedNever();
        }
    }
}

/// <summary>
/// Unit tests for Story 3.1: SyncRunsController summary view.
/// </summary>
public class SyncRunSummaryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestableServerDbContextForSyncRuns _db;

    // Stable user GUIDs matching DatabaseSeeder pattern
    private static readonly Guid User1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid User2 = new("00000000-0000-0000-0000-000000000002");

    public SyncRunSummaryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new TestableServerDbContextForSyncRuns(options);
        _db.Database.EnsureCreated();

        // Seed User rows so FK constraint on SyncRun.UserId is satisfied
        _db.Users.AddRange(
            new User { Id = User1, Username = "user-1", Email = "user1@test.com" },
            new User { Id = User2, Username = "user-2", Email = "user2@test.com" }
        );
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private SyncRunsController CreateController() =>
        new SyncRunsController(_db, NullLogger<SyncRunsController>.Instance);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SyncRun MakePushRun(Guid userId, DateTime occurredAt, int count = 5, string status = "success") =>
        new SyncRun
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            RunType = "push",
            UserId = userId,
            MeasurementCount = count,
            Status = status
        };

    private SyncRun MakePullRun(DateTime occurredAt, int count = 10) =>
        new SyncRun
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            RunType = "pull",
            UserId = null,
            MeasurementCount = count,
            Status = "success"
        };

    private static T GetValue<T>(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName)
                   ?? throw new InvalidOperationException($"Property '{propertyName}' not found.");
        return (T)prop.GetValue(obj)!;
    }

    private static List<dynamic> GetRuns(OkObjectResult ok)
    {
        var value = ok.Value!;
        var runsProp = value.GetType().GetProperty("runs")
                       ?? throw new InvalidOperationException("Property 'runs' not found.");
        return ((IEnumerable<object>)runsProp.GetValue(value)!).Cast<dynamic>().ToList();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecent_NoRuns_ReturnsEmptyList()
    {
        var controller = CreateController();
        var result = await controller.GetRecent(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var runs = GetRuns(ok);
        Assert.Empty(runs);
    }

    [Fact]
    public async Task GetRecent_AfterTwoPushRuns_ReturnsBothNewestFirst()
    {
        var older = MakePushRun(User1, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        var newer = MakePushRun(User1, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        _db.SyncRuns.AddRange(older, newer);
        await _db.SaveChangesAsync();

        var controller = CreateController();
        var result = await controller.GetRecent(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var runs = GetRuns(ok);
        Assert.Equal(2, runs.Count);
        // Newest first: newer OccurredAt should be first
        var firstOccurredAt = GetValue<DateTime>((object)runs[0], "OccurredAt");
        var secondOccurredAt = GetValue<DateTime>((object)runs[1], "OccurredAt");
        Assert.True(firstOccurredAt > secondOccurredAt);
    }

    [Fact]
    public async Task GetRecent_FilterByUserId_ReturnsOnlyThatUser()
    {
        _db.SyncRuns.AddRange(
            MakePushRun(User1, DateTime.UtcNow.AddMinutes(-3)),
            MakePushRun(User1, DateTime.UtcNow.AddMinutes(-2)),
            MakePushRun(User2, DateTime.UtcNow.AddMinutes(-1))
        );
        await _db.SaveChangesAsync();

        var controller = CreateController();
        var result = await controller.GetRecent(userId: User1, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var runs = GetRuns(ok);
        Assert.Equal(2, runs.Count);
        foreach (var r in runs)
            Assert.Equal((Guid?)User1, GetValue<Guid?>((object)r, "UserId"));
    }

    [Fact]
    public async Task GetRecent_FilterByRunType_ReturnsOnlyThatType()
    {
        _db.SyncRuns.AddRange(
            MakePushRun(User1, DateTime.UtcNow.AddMinutes(-3)),
            MakePushRun(User1, DateTime.UtcNow.AddMinutes(-2)),
            MakePullRun(DateTime.UtcNow.AddMinutes(-1))
        );
        await _db.SaveChangesAsync();

        var controller = CreateController();
        var result = await controller.GetRecent(runType: "push", cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var runs = GetRuns(ok);
        Assert.Equal(2, runs.Count);
        foreach (var r in runs)
            Assert.Equal("push", GetValue<string>((object)r, "RunType"));
    }

    [Fact]
    public async Task GetRecent_InvalidRunType_Returns400()
    {
        var controller = CreateController();
        var result = await controller.GetRecent(runType: "invalid", cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetRecent_FailedRun_IncludesErrorMessage()
    {
        var failedRun = new SyncRun
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            RunType = "push",
            UserId = User1,
            MeasurementCount = 0,
            Status = "failed",
            ErrorMessage = "Connection dropped"
        };
        _db.SyncRuns.Add(failedRun);
        await _db.SaveChangesAsync();

        var controller = CreateController();
        var result = await controller.GetRecent(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var runs = GetRuns(ok);
        Assert.Single(runs);
        Assert.Equal("failed", GetValue<string>((object)runs[0], "Status"));
        Assert.NotNull(GetValue<string?>((object)runs[0], "ErrorMessage"));
    }
}
