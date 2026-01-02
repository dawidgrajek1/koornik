using Koornik.Application.Interfaces;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Koornik.Domain.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Koornik.Infrastructure.Services;

/// <summary>
/// Background service that creates hourly snapshots of zone occupancy state.
/// On startup, creates initial snapshots for zones that don't have any.
/// </summary>
public class ZoneSnapshotService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ZoneSnapshotService> _logger;
    private readonly TimeSpan _snapshotInterval = TimeSpan.FromHours(1);

    public ZoneSnapshotService(
        IServiceScopeFactory scopeFactory,
        ILogger<ZoneSnapshotService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Zone Snapshot Service starting...");

        try
        {
            // Create initial snapshots for zones without any
            await CreateInitialSnapshotsAsync(stoppingToken);

            // Start periodic snapshot creation
            using var timer = new PeriodicTimer(_snapshotInterval);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                    await CreateSnapshotsForAllZonesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating zone snapshots. Will retry on next interval.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Zone Snapshot Service.");
            throw;
        }

        _logger.LogInformation("Zone Snapshot Service stopped.");
    }

    private async Task CreateInitialSnapshotsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Checking for zones without snapshots...");

        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var zoneService = scope.ServiceProvider.GetRequiredService<IZoneService>();

        var zones = await unitOfWork.Repository<Zone>().ListAll();
        var snapshotsCreated = 0;

        foreach (var zone in zones)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            // Check if zone has any snapshots
            var existingSnapshotSpec = ZoneStateSnapshotSpecifications.GetAnyByZone(zone.Id);
            var existingSnapshots = await unitOfWork.Repository<ZoneStateSnapshot>().List(existingSnapshotSpec);

            if (!existingSnapshots.Any())
            {
                _logger.LogInformation("Creating initial snapshot for zone {ZoneName} ({ZoneId})", zone.Name, zone.Id);

                // Calculate current occupancy from all transitions
                var occupancy = await zoneService.CalculateCurrentOccupancy(zone.Id);

                var snapshot = new ZoneStateSnapshot
                {
                    Id = Guid.NewGuid(),
                    ZoneId = zone.Id,
                    CreatedAt = DateTime.UtcNow,
                    OccupancyCount = occupancy,
                    Capacity = zone.Capacity
                };

                await unitOfWork.Repository<ZoneStateSnapshot>().Add(snapshot);
                snapshotsCreated++;
            }
        }

        if (snapshotsCreated > 0)
        {
            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Created {Count} initial zone snapshots.", snapshotsCreated);
        }
        else
        {
            _logger.LogInformation("All zones already have snapshots.");
        }
    }

    private async Task CreateSnapshotsForAllZonesAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Creating hourly snapshots for all zones...");

        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var zoneService = scope.ServiceProvider.GetRequiredService<IZoneService>();

        var zones = await unitOfWork.Repository<Zone>().ListAll();
        var timestamp = DateTime.UtcNow;
        var snapshotsCreated = 0;

        foreach (var zone in zones)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                var occupancy = await zoneService.CalculateCurrentOccupancy(zone.Id);

                var snapshot = new ZoneStateSnapshot
                {
                    Id = Guid.NewGuid(),
                    ZoneId = zone.Id,
                    CreatedAt = timestamp,
                    OccupancyCount = occupancy,
                    Capacity = zone.Capacity
                };

                await unitOfWork.Repository<ZoneStateSnapshot>().Add(snapshot);
                snapshotsCreated++;

                _logger.LogDebug("Created snapshot for zone {ZoneName}: Occupancy={Occupancy}/{Capacity}",
                    zone.Name, occupancy, zone.Capacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create snapshot for zone {ZoneName} ({ZoneId})", zone.Name, zone.Id);
            }
        }

        if (snapshotsCreated > 0)
        {
            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Created {Count} hourly zone snapshots at {Timestamp:O}", snapshotsCreated, timestamp);
        }
    }
}
