using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServerService.Controllers;
using Sync.Domain.Entities;
using Sync.Infrastructure.Data;
using Xunit;

namespace MicroservicesSync.Tests.MeasurementsGrid;

/// <summary>
/// SQLite-compatible subclass of ServerDbContext for MeasurementsGrid unit tests.
/// Overrides RowVersion columns to ValueGeneratedNever so SQLite accepts
/// Array.Empty&lt;byte&gt;() as the default value.
/// </summary>
internal sealed class TestableServerDbContextForGrid : ServerDbContext
{
    public TestableServerDbContextForGrid(DbContextOptions<ServerDbContext> options) : base(options) { }

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
/// Unit tests for Story 3.2: MeasurementsGridController paged/filtered/sorted queries.
/// </summary>
public class MeasurementsGridPagingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestableServerDbContextForGrid _db;

    // Stable GUIDs matching DatabaseSeeder pattern
    private static readonly Guid User1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid User2 = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Building1 = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Room1 = new("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Surface1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid Cell1 = new("40000000-0000-0000-0000-000000000001");

    public MeasurementsGridPagingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new TestableServerDbContextForGrid(options);
        _db.Database.EnsureCreated();

        // Seed full FK chain: User → Building → Room → Surface → Cell
        _db.Users.AddRange(
            new User { Id = User1, Username = "user1", Email = "user1@test.com" },
            new User { Id = User2, Username = "user2", Email = "user2@test.com" }
        );
        _db.Buildings.Add(new Building { Id = Building1, Identifier = "building-alpha" });
        _db.Rooms.Add(new Room { Id = Room1, Identifier = "room-a1", BuildingId = Building1 });
        _db.Surfaces.Add(new Surface { Id = Surface1, Identifier = "surface-a1-s1", RoomId = Room1 });
        _db.Cells.Add(new Cell { Id = Cell1, Identifier = "cell-a1s1-c1", SurfaceId = Surface1 });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private MeasurementsGridController CreateController() =>
        new MeasurementsGridController(_db);

    private Measurement MakeMeasurement(decimal value, DateTime recordedAt, Guid? userId = null) =>
        new Measurement
        {
            Id = Guid.NewGuid(),
            Value = value,
            RecordedAt = recordedAt,
            SyncedAt = null,
            UserId = userId ?? User1,
            CellId = Cell1
        };

    private static T GetProp<T>(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name)
                   ?? throw new InvalidOperationException($"Property '{name}' not found.");
        return (T)prop.GetValue(obj)!;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPaged_NoFilters_ReturnsFirstPage()
    {
        // Seed 15 measurements
        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
            _db.Measurements.Add(MakeMeasurement(i + 1, now.AddMinutes(-i)));
        await _db.SaveChangesAsync();

        var controller = CreateController();
        var result = await controller.GetPaged(page: 1, pageSize: 10, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        Assert.Equal(10, GetProp<System.Collections.IList>(value, "data").Count);
        Assert.Equal(15, GetProp<int>(value, "totalCount"));
        Assert.Equal(2, GetProp<int>(value, "totalPages"));
        Assert.Equal(1, GetProp<int>(value, "page"));
    }

    [Fact]
    public async Task GetPaged_Page2_ReturnsRemainder()
    {
        var now = DateTime.UtcNow;
        for (int i = 0; i < 15; i++)
            _db.Measurements.Add(MakeMeasurement(i + 1, now.AddMinutes(-i)));
        await _db.SaveChangesAsync();

        var controller = CreateController();
        var result = await controller.GetPaged(page: 2, pageSize: 10, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(5, GetProp<System.Collections.IList>(ok.Value!, "data").Count);
    }

    [Fact]
    public async Task GetPaged_FilterByUserId_ReturnsOnlyMatchingRows()
    {
        var now = DateTime.UtcNow;
        // 7 for User1, 3 for User2
        for (int i = 0; i < 7; i++)
            _db.Measurements.Add(MakeMeasurement(i + 1, now.AddMinutes(-i), User1));
        for (int i = 0; i < 3; i++)
            _db.Measurements.Add(MakeMeasurement(i + 100, now.AddMinutes(-i - 10), User2));
        await _db.SaveChangesAsync();

        var filters = $"{{\"groupOp\":\"AND\",\"rules\":[{{\"field\":\"userId\",\"op\":\"eq\",\"data\":\"{User1}\"}}]}}";
        var controller = CreateController();
        var result = await controller.GetPaged(page: 1, pageSize: 50, filters: filters, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(7, GetProp<int>(ok.Value!, "totalCount"));
        Assert.Equal(7, GetProp<System.Collections.IList>(ok.Value!, "data").Count);
    }

    [Fact]
    public async Task GetPaged_FilterByValueRange_ReturnsCorrectSubset()
    {
        var now = DateTime.UtcNow;
        foreach (var v in new[] { 1.0m, 2.0m, 3.0m, 4.0m, 5.0m })
            _db.Measurements.Add(MakeMeasurement(v, now.AddMinutes(-(int)v)));
        await _db.SaveChangesAsync();

        // Filter: value gt 2.0 → should return 3.0, 4.0, 5.0
        var filters = "{\"groupOp\":\"AND\",\"rules\":[{\"field\":\"value\",\"op\":\"gt\",\"data\":\"2.0\"}]}";
        var controller = CreateController();
        var result = await controller.GetPaged(page: 1, pageSize: 50, filters: filters, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(3, GetProp<int>(ok.Value!, "totalCount"));
    }

    [Fact]
    public async Task GetPaged_SortByValueDesc_ReturnsDescending()
    {
        var now = DateTime.UtcNow;
        foreach (var v in new[] { 3.0m, 1.0m, 5.0m, 2.0m, 4.0m })
            _db.Measurements.Add(MakeMeasurement(v, now.AddMinutes(-(int)v)));
        await _db.SaveChangesAsync();

        var controller = CreateController();
        var result = await controller.GetPaged(page: 1, pageSize: 10, sortBy: "value", sortOrder: "desc", cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetProp<System.Collections.IList>(ok.Value!, "data");
        Assert.Equal(5, data.Count);

        var first = data[0]!;
        var second = data[1]!;
        var firstValue = GetProp<decimal>(first, "Value");
        var secondValue = GetProp<decimal>(second, "Value");
        Assert.True(firstValue >= secondValue, $"Expected descending order but got {firstValue} before {secondValue}");
    }

    [Fact]
    public async Task GetPaged_InvalidFilterJson_Returns400()
    {
        var controller = CreateController();
        var result = await controller.GetPaged(filters: "not-json", cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
