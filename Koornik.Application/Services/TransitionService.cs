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

public class TransitionService(IUnitOfWork unitOfWork, ILogger<TransitionService> logger) : ITransitionService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<TransitionService> _logger = logger;

    public async Task<ErrorOr<TransitionResponse>> CreateTransition(CreateTransitionRequest request)
    {
        List<Error> errors = [];

        if (request.DeviceId == Guid.Empty)
        {
            errors.Add(Error.Validation(description: "Device id should not be empty."));
        }

        // At least one zone must be specified for a valid transition
        if ((request.FromZoneId is null || request.FromZoneId == Guid.Empty) && 
            (request.ToZoneId is null || request.ToZoneId == Guid.Empty))
        {
            errors.Add(Error.Validation(description: "At least one zone (source or destination) must be specified."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        // Fetch zones to update their CurrentOccupancy (only if specified)
        Zone? fromZone = null;
        Zone? toZone = null;

        if (request.FromZoneId.HasValue && request.FromZoneId != Guid.Empty)
        {
            fromZone = await _unitOfWork.Repository<Zone>().GetById(request.FromZoneId.Value);
            if (fromZone is null)
            {
                return Error.NotFound(description: "Source zone not found.");
            }
        }

        if (request.ToZoneId.HasValue && request.ToZoneId != Guid.Empty)
        {
            toZone = await _unitOfWork.Repository<Zone>().GetById(request.ToZoneId.Value);
            if (toZone is null)
            {
                return Error.NotFound(description: "Destination zone not found.");
            }
        }

        // Update occupancy counts only for tracked zones
        if (fromZone is not null)
        {
            fromZone.CurrentOccupancy = Math.Max(0, fromZone.CurrentOccupancy - 1);
            _unitOfWork.Repository<Zone>().Update(fromZone);
        }

        if (toZone is not null)
        {
            toZone.CurrentOccupancy += 1;
            _unitOfWork.Repository<Zone>().Update(toZone);
        }

        var transition = await _unitOfWork.Repository<Transition>().Add(new Transition
        {
            DeviceId = request.DeviceId,
            FromZone = request.FromZoneId == Guid.Empty ? null : request.FromZoneId,
            ToZone = request.ToZoneId == Guid.Empty ? null : request.ToZoneId,
            CreatedAt = request.CreatedAt ?? DateTime.UtcNow,
        });

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Transition created: FromZone {FromZoneId} (occupancy: {FromOccupancy}) -> ToZone {ToZoneId} (occupancy: {ToOccupancy})",
            fromZone?.Id.ToString() ?? "Outside", fromZone?.CurrentOccupancy ?? 0, 
            toZone?.Id.ToString() ?? "Outside", toZone?.CurrentOccupancy ?? 0);

        return transition.ToResponse();
    }

    public async Task<ErrorOr<IEnumerable<TransitionResponse>>> GetAllTransitions()
    {
        var transitions = await _unitOfWork.Repository<Transition>().ListAll();
        return transitions.Select(x => x.ToResponse()).ToList();
    }

    public async Task<ErrorOr<TransitionResponse>> GetTransitionById(Guid id)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation(description: "Transition id should not be empty.");
        }

        var transition = await _unitOfWork.Repository<Transition>().GetById(id);

        if (transition is null)
        {
            return Error.NotFound(description: "Transition not found.");
        }

        return transition.ToResponse();
    }

    public async Task<ErrorOr<IEnumerable<TransitionResponse>>> GetTransitionsForZone(Guid zoneId, DateTime? start = null, DateTime? end = null)
    {
        if (zoneId == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }

        IEnumerable<Transition> transitions;

        if (start.HasValue && end.HasValue)
        {
            // Ensure dates are UTC for PostgreSQL compatibility
            var startUtc = DateTime.SpecifyKind(start.Value, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end.Value, DateTimeKind.Utc);
            var spec = TransitionSpecifications.GetByZoneAndDateRange(zoneId, startUtc, endUtc);
            transitions = await _unitOfWork.Repository<Transition>().List(spec);
        }
        else
        {
            var spec = TransitionSpecifications.GetByZoneId(zoneId);
            transitions = await _unitOfWork.Repository<Transition>().List(spec);
        }

        return transitions.Select(x => x.ToResponse()).ToList();
    }

    public async Task<ErrorOr<IEnumerable<TransitionResponse>>> GetTransitionsForDevice(Guid deviceId, DateTime? start = null, DateTime? end = null)
    {
        if (deviceId == Guid.Empty)
        {
            return Error.Validation(description: "Device id should not be empty.");
        }

        IEnumerable<Transition> transitions;

        if (start.HasValue && end.HasValue)
        {
            // Ensure dates are UTC for PostgreSQL compatibility
            var startUtc = DateTime.SpecifyKind(start.Value, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end.Value, DateTimeKind.Utc);
            var spec = TransitionSpecifications.GetByDeviceAndDateRange(deviceId, startUtc, endUtc);
            transitions = await _unitOfWork.Repository<Transition>().List(spec);
        }
        else
        {
            var spec = TransitionSpecifications.GetByDeviceId(deviceId);
            transitions = await _unitOfWork.Repository<Transition>().List(spec);
        }

        return transitions.Select(x => x.ToResponse()).ToList();
    }

    public async Task<ErrorOr<IEnumerable<TransitionResponse>>> GetTransitionsForTimeRange(DateTime start, DateTime end)
    {
        // Ensure dates are UTC for PostgreSQL compatibility
        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        var spec = TransitionSpecifications.GetByDateRange(start, end);
        var transitions = await _unitOfWork.Repository<Transition>().List(spec);

        return transitions.Select(x => x.ToResponse()).ToList();
    }
}