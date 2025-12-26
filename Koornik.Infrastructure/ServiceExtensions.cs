using Koornik.Application.Core.Services;
using Koornik.Domain.Core.Repositories;
using Koornik.Infrastructure.Data;
using Koornik.Infrastructure.HealthChecks;
using Koornik.Infrastructure.Repositories;
using Koornik.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Polly;
using Serilog;
namespace Koornik.Infrastructure;

public static class ServiceExtensions
{
    public static void ConfigureInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KoornikDB");
        services.AddDbContextPool<KoornikDbContext>(options =>
            options.UseNpgsql(connectionString,
                x => x.MigrationsAssembly("Koornik.Infrastructure")
                    .CommandTimeout(30)));

        // Add DbContext factory for health checks
        services.AddDbContextFactory<KoornikDbContext>(options =>
            options.UseNpgsql(connectionString,
                x => x.MigrationsAssembly("Koornik.Infrastructure")
                    .CommandTimeout(30)));

        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ILoggerService, LoggerService>();

        // Register health checks
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        // Register MQTT service as singleton and hosted service to run broker on startup
        services.AddSingleton<MqttService>();
        services.AddHostedService<MqttHostedService>();
    }

    /// <summary>
    /// Migrates the database with retry policy (5 retries with exponential backoff).
    /// If all retries fail, an exception is thrown which should be handled by the caller.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider serviceProvider)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<KoornikDbContext>>();

        // Define retry policy: 5 retries with exponential backoff (1s, 2s, 4s, 8s, 16s)
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    Log.Warning(
                        "Database migration attempt {RetryCount} failed. Retrying in {DelaySeconds} seconds. Error: {ErrorMessage}",
                        retryCount,
                        timespan.TotalSeconds,
                        exception.Message);
                });

        try
        {
            await retryPolicy.ExecuteAsync(async () =>
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                Log.Information("Attempting database migration...");
                await dbContext.Database.MigrateAsync();
                Log.Information("Database migration completed successfully.");
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database migration failed after all retry attempts.");
            throw;
        }
    }
}