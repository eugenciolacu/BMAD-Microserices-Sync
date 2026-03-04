using Sync.Application.Options;
using Xunit;

namespace MicroservicesSync.Tests.Options;

/// <summary>
/// Unit tests for SyncOptions POCO — Story 1.3.
/// Verifies default values and section name constant so that any accidental
/// refactoring of defaults or the binding key is caught immediately.
/// </summary>
public class SyncOptionsTests
{
    [Fact]
    public void SectionName_IsCorrectBindingKey()
    {
        // Both Program.cs files use SyncOptions.SectionName as the config section key.
        // If this value changes, the IOptions<SyncOptions> binding silently breaks.
        Assert.Equal("SyncOptions", SyncOptions.SectionName);
    }

    [Fact]
    public void DefaultBatchSize_IsFive()
    {
        var options = new SyncOptions();

        Assert.Equal(5, options.BatchSize);
    }

    [Fact]
    public void DefaultMeasurementsPerClient_IsTen()
    {
        var options = new SyncOptions();

        Assert.Equal(10, options.MeasurementsPerClient);
    }

    [Fact]
    public void BatchSize_CanBeOverridden()
    {
        var options = new SyncOptions { BatchSize = 20 };

        Assert.Equal(20, options.BatchSize);
    }

    [Fact]
    public void MeasurementsPerClient_CanBeOverridden()
    {
        var options = new SyncOptions { MeasurementsPerClient = 100 };

        Assert.Equal(100, options.MeasurementsPerClient);
    }
}
