using ErrorOr;
using Koornik.Application.Interfaces;
using Koornik.Application.Mapping;
using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Koornik.Domain.Specifications;
using Microsoft.Extensions.Logging;

namespace Koornik.Application.Services;

public class ZoneService(IUnitOfWork unitOfWork, ILogger<ZoneService> logger) : IZoneService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ZoneService> _logger = logger;

    public async Task<ErrorOr<ZoneResponse>> CreateZone(CreateZoneRequest request)
    {
        List<Error> errors = [];

        if (string.IsNullOrEmpty(request.Name))
        {
            errors.Add(Error.Validation(description: "Zone name should not be empty."));
        }

        if (request.Capacity < 1)
        {
            errors.Add(Error.Validation(description: "Zone capacity should not be less than 1."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var zone = await _unitOfWork.Repository<Zone>().Add(new Zone
        {
            Name = request.Name,
            Capacity = request.Capacity,
        });

        await _unitOfWork.SaveChangesAsync();
        return zone.ToResponse();
    }

    public async Task<ErrorOr<ZoneResponse>> GetZoneById(Guid id)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }

        var zone = await _unitOfWork.Repository<Zone>().GetById(id);

        if (zone is null)
        {
            return Error.NotFound(description: "Zone not found.");
        }

        return zone.ToResponse();
    }

    public async Task<ErrorOr<ZoneResponse>> UpdateZone(Guid id, UpdateZoneRequest request)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }

        var zone = await _unitOfWork.Repository<Zone>().GetById(id);

        if (zone is null)
        {
            return Error.NotFound(description: "Zone not found.");
        }

        zone.Name = request.Name;
        zone.Capacity = request.Capacity;

        _unitOfWork.Repository<Zone>().Update(zone);
        await _unitOfWork.SaveChangesAsync();

        return zone.ToResponse();
    }

    public async Task<ErrorOr<IEnumerable<ZoneResponse>>> GetAllZones()
    {
        var zones = await _unitOfWork.Repository<Zone>().ListAll();
        return zones.Select(x => x.ToResponse()).ToList();
    }

    public async Task<ErrorOr<Deleted>> DeleteZone(Guid id)
    {
        var zone = await _unitOfWork.Repository<Zone>().GetById(id);
        if (zone is null)
        {
            return Result.Deleted;
        }

        _unitOfWork.Repository<Zone>().Delete(zone);
        await _unitOfWork.SaveChangesAsync();

        return Result.Deleted;
    }

    public async Task<ErrorOr<ZoneStateResponse>> GetCurrentState(Guid zoneId)
    {
        if (zoneId == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }

        var zone = await _unitOfWork.Repository<Zone>().GetById(zoneId);
        if (zone is null)
        {
            return Error.NotFound(description: "Zone not found.");
        }

        var occupancyCount = await CalculateCurrentOccupancy(zoneId);

        return new ZoneStateResponse
        {
            ZoneId = zone.Id,
            ZoneName = zone.Name,
            Timestamp = DateTime.UtcNow,
            OccupancyCount = occupancyCount,
            Capacity = zone.Capacity
        };
    }

    public async Task<ErrorOr<ZoneStateResponse>> GetStateAt(Guid zoneId, DateTime timestamp)
    {
        if (zoneId == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }

        var zone = await _unitOfWork.Repository<Zone>().GetById(zoneId);
        if (zone is null)
        {
            return Error.NotFound(description: "Zone not found.");
        }

        var occupancyCount = await CalculateOccupancyAtTimestamp(zoneId, timestamp);

        return new ZoneStateResponse
        {
            ZoneId = zone.Id,
            ZoneName = zone.Name,
            Timestamp = timestamp,
            OccupancyCount = occupancyCount,
            Capacity = zone.Capacity
        };
    }

    public async Task<ErrorOr<ZoneStateHistoryResponse>> GetStateHistory(Guid zoneId, DateTime startDate, DateTime endDate)
    {
        if (zoneId == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }

        if (startDate > endDate)
        {
            return Error.Validation(description: "Start date must be before end date.");
        }

        var zone = await _unitOfWork.Repository<Zone>().GetById(zoneId);
        if (zone is null)
        {
            return Error.NotFound(description: "Zone not found.");
        }

        var snapshotsSpec = ZoneStateSnapshotSpecifications.GetByZoneAndDateRange(zoneId, startDate, endDate);
        var snapshots = await _unitOfWork.Repository<ZoneStateSnapshot>().List(snapshotsSpec);

        var states = snapshots.Select(s => new ZoneStateResponse
        {
            ZoneId = s.ZoneId,
            ZoneName = zone.Name,
            Timestamp = s.CreatedAt,
            OccupancyCount = s.OccupancyCount,
            Capacity = s.Capacity
        }).ToList();

        return new ZoneStateHistoryResponse
        {
            ZoneId = zone.Id,
            ZoneName = zone.Name,
            StartDate = startDate,
            EndDate = endDate,
            States = states
        };
    }

    public async Task<int> CalculateCurrentOccupancy(Guid zoneId)
    {
        return await CalculateOccupancyAtTimestamp(zoneId, DateTime.UtcNow);
    }

    private async Task<int> CalculateOccupancyAtTimestamp(Guid zoneId, DateTime timestamp)
    {
        // Get the latest snapshot before the timestamp
        var snapshotSpec = ZoneStateSnapshotSpecifications.GetLatestBeforeTimestamp(zoneId, timestamp);
        var snapshots = await _unitOfWork.Repository<ZoneStateSnapshot>().List(snapshotSpec);
        var latestSnapshot = snapshots.FirstOrDefault();

        int baseCount = 0;
        DateTime startTime = DateTime.MinValue;

        if (latestSnapshot is not null)
        {
            baseCount = latestSnapshot.OccupancyCount;
            startTime = latestSnapshot.CreatedAt;
        }

        // Get all transitions since the snapshot (or since beginning if no snapshot)
        var transitionsSpec = TransitionSpecifications.GetByZoneAndDateRange(zoneId, startTime, timestamp);
        var transitions = await _unitOfWork.Repository<Transition>().List(transitionsSpec);

        // Calculate delta: +1 for each entry (ToZone), -1 for each exit (FromZone)
        int delta = 0;
        foreach (var transition in transitions)
        {
            if (transition.ToZone == zoneId)
            {
                delta++;
            }
            if (transition.FromZone == zoneId)
            {
                delta--;
            }
        }

        // Ensure count doesn't go negative
        return Math.Max(0, baseCount + delta);
    }
}