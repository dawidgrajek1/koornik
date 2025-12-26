using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Koornik.Infrastructure.Data;

namespace Koornik.Infrastructure.HealthChecks;

/// <summary>
/// Health check for database connectivity.
/// This class is designed to be extended with additional dependency checks in the future.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<KoornikDbContext> _contextFactory;

    public DatabaseHealthCheck(IDbContextFactory<KoornikDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database connection is healthy.")
                : HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Database health check failed: {ex.Message}");
        }
    }
}
