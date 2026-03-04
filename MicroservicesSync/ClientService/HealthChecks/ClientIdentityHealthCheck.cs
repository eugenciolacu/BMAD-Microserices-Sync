using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClientService.HealthChecks;

/// <summary>
/// Validates that ClientIdentity:UserId is present and is a non-empty GUID.
/// Returns Unhealthy (HTTP 503) if missing or invalid — AC#2 of Story 1.2.
/// </summary>
internal static class ClientIdentityHealthCheck
{
    internal static HealthCheckResult Evaluate(string? userId) =>
        Guid.TryParse(userId, out var guid) && guid != Guid.Empty
            ? HealthCheckResult.Healthy("ClientIdentity:UserId is configured")
            : HealthCheckResult.Unhealthy("ClientIdentity:UserId is missing or invalid");
}
