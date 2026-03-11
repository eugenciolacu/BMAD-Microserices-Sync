using Sync.Infrastructure.Grid;
using Xunit;

namespace MicroservicesSync.Tests.MeasurementsGrid;

/// <summary>
/// Unit tests for JqGridHelper filter/sort expression builder.
/// Uses in-memory List&lt;T&gt;.AsQueryable() — no database needed.
/// </summary>
public class JqGridHelperTests
{
    private class TestEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int Count { get; set; }
    }

    private static JqGridFilter MakeFilter(string field, string op, string data, string groupOp = "AND") =>
        new JqGridFilter
        {
            GroupOp = groupOp,
            Rules = [new JqGridFilterRule { Field = field, Op = op, Data = data }]
        };

    // ── ApplyFilters tests ────────────────────────────────────────────────────

    [Fact]
    public void ApplyFilters_GuidEq_FiltersCorrectly()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var items = new[] {
            new TestEntity { Id = id1, Name = "A" },
            new TestEntity { Id = id2, Name = "B" },
            new TestEntity { Id = id3, Name = "C" }
        }.AsQueryable();

        var filter = MakeFilter("id", "eq", id2.ToString());
        var result = JqGridHelper.ApplyFilters(items, filter).ToList();

        Assert.Single(result);
        Assert.Equal(id2, result[0].Id);
    }

    [Fact]
    public void ApplyFilters_DecimalGt_FiltersCorrectly()
    {
        var items = new[] {
            new TestEntity { Value = 1.0m },
            new TestEntity { Value = 2.0m },
            new TestEntity { Value = 3.0m }
        }.AsQueryable();

        var filter = MakeFilter("value", "gt", "1.5");
        var result = JqGridHelper.ApplyFilters(items, filter).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.True(r.Value > 1.5m));
    }

    [Fact]
    public void ApplyFilters_DateTimeGe_FiltersCorrectly()
    {
        var d1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var d3 = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new[] {
            new TestEntity { CreatedAt = d1 },
            new TestEntity { CreatedAt = d2 },
            new TestEntity { CreatedAt = d3 }
        }.AsQueryable();

        var filter = MakeFilter("createdAt", "ge", "2026-06-01T00:00:00Z");
        var result = JqGridHelper.ApplyFilters(items, filter).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.True(r.CreatedAt >= d2));
    }

    [Fact]
    public void ApplyFilters_StringContains_FiltersCorrectly()
    {
        var items = new[] {
            new TestEntity { Name = "Alpha" },
            new TestEntity { Name = "Beta" },
            new TestEntity { Name = "AlphaTwo" }
        }.AsQueryable();

        var filter = MakeFilter("name", "cn", "Alpha");
        var result = JqGridHelper.ApplyFilters(items, filter).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Contains("Alpha", r.Name));
    }

    [Fact]
    public void ApplyFilters_InvalidFieldName_IgnoredSafely()
    {
        var items = new[] {
            new TestEntity { Name = "A" },
            new TestEntity { Name = "B" }
        }.AsQueryable();

        var filter = MakeFilter("nonexistent", "eq", "somevalue");
        var result = JqGridHelper.ApplyFilters(items, filter).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyFilters_InvalidGuidData_IgnoredSafely()
    {
        var items = new[] {
            new TestEntity { Id = Guid.NewGuid() },
            new TestEntity { Id = Guid.NewGuid() }
        }.AsQueryable();

        var filter = MakeFilter("id", "eq", "not-a-guid");
        var result = JqGridHelper.ApplyFilters(items, filter).ToList();

        Assert.Equal(2, result.Count);
    }

    // ── ApplySort tests ───────────────────────────────────────────────────────

    [Fact]
    public void ApplySort_SortByValueDesc_SortsCorrectly()
    {
        var items = new[] {
            new TestEntity { Value = 3.0m },
            new TestEntity { Value = 1.0m },
            new TestEntity { Value = 2.0m }
        }.AsQueryable();

        var result = JqGridHelper.ApplySort(items, "value", "desc").ToList();

        Assert.Equal(3.0m, result[0].Value);
        Assert.Equal(2.0m, result[1].Value);
        Assert.Equal(1.0m, result[2].Value);
    }

    [Fact]
    public void ApplySort_SortByValueAsc_SortsCorrectly()
    {
        var items = new[] {
            new TestEntity { Value = 3.0m },
            new TestEntity { Value = 1.0m },
            new TestEntity { Value = 2.0m }
        }.AsQueryable();

        var result = JqGridHelper.ApplySort(items, "value", "asc").ToList();

        Assert.Equal(1.0m, result[0].Value);
        Assert.Equal(2.0m, result[1].Value);
        Assert.Equal(3.0m, result[2].Value);
    }

    [Fact]
    public void ApplySort_InvalidSortField_ReturnsUnchanged()
    {
        var items = new[] {
            new TestEntity { Value = 3.0m },
            new TestEntity { Value = 1.0m }
        }.AsQueryable();

        var result = JqGridHelper.ApplySort(items, "nonexistent", "asc").ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyFilters_EmptyRules_ReturnsAll()
    {
        var items = new[] {
            new TestEntity { Name = "A" },
            new TestEntity { Name = "B" }
        }.AsQueryable();

        var filter = new JqGridFilter { GroupOp = "AND", Rules = [] };
        var result = JqGridHelper.ApplyFilters(items, filter).ToList();

        Assert.Equal(2, result.Count);
    }
}
