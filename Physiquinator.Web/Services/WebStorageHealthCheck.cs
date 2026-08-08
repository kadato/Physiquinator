using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Physiquinator.Web.Services;

/// <summary>
/// Readiness probe: verifies the ephemeral storage directory is present and writable,
/// which is what the web host depends on for its SQLite databases.
/// </summary>
public sealed class WebStorageHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = WebDatabasePathProvider.ResolveDatabaseDirectory();
            var probePath = Path.Combine(directory, $"healthz-{Guid.NewGuid():N}.probe");
            await File.WriteAllTextAsync(probePath, "ok", cancellationToken);
            File.Delete(probePath);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Storage directory is not writable.", ex);
        }
    }
}
