namespace Sync.Application.Options;

/// <summary>
/// Configurable parameters controlling Microserices-Sync experiment scenarios.
/// Bind via "SyncOptions" configuration section or SyncOptions__* environment variables.
/// </summary>
public class SyncOptions
{
    /// <summary>
    /// The configuration section key used to bind this class via
    /// <c>builder.Services.Configure&lt;SyncOptions&gt;(configuration.GetSection(SyncOptions.SectionName))</c>.
    /// Matches the "SyncOptions" key in appsettings.json and the SyncOptions__* env-var prefix.
    /// </summary>
    public const string SectionName = "SyncOptions";

    /// <summary>
    /// Number of measurements to generate per ClientService instance in a single scenario run.
    /// Override via environment variable: SyncOptions__MeasurementsPerClient
    /// Default: 10
    /// </summary>
    public int MeasurementsPerClient { get; set; } = 10;

    /// <summary>
    /// Number of measurement records per in-memory batch during a push or pull sync operation.
    /// The entire push/pull still executes inside a single DB transaction across all batches.
    /// Override via environment variable: SyncOptions__BatchSize
    /// Default: 5
    /// </summary>
    public int BatchSize { get; set; } = 5;
}
