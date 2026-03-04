using ClientService.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace MicroservicesSync.Tests.HealthChecks;

/// <summary>
/// Unit tests for ClientIdentityHealthCheck — Story 1.2, AC#2.
/// Verifies that a valid non-empty GUID returns Healthy and
/// anything missing/empty/invalid returns Unhealthy (HTTP 503).
/// </summary>
public class ClientIdentityHealthCheckTests
{
    // ── Healthy cases ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    public void Evaluate_WithValidNonEmptyGuid_ReturnsHealthy(string userId)
    {
        var result = ClientIdentityHealthCheck.Evaluate(userId);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("ClientIdentity:UserId is configured", result.Description);
    }

    // ── Unhealthy cases ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]                                     // not set
    [InlineData("")]                                       // empty string
    [InlineData("   ")]                                    // whitespace
    [InlineData("not-a-guid")]                             // non-GUID string
    [InlineData("00000000-0000-0000-0000-000000000000")]   // Guid.Empty
    public void Evaluate_WithInvalidOrMissingUserId_ReturnsUnhealthy(string? userId)
    {
        var result = ClientIdentityHealthCheck.Evaluate(userId);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("ClientIdentity:UserId is missing or invalid", result.Description);
    }
}
