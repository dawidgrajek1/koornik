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

        if (request.FromZoneId == Guid.Empty)
        {
            errors.Add(Error.Validation(description: "Source zone id should not be empty."));
        }

        if (request.ToZoneId == Guid.Empty)
        {
            errors.Add(Error.Validation(description: "Destination zone id should not be empty."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        // Fetch the zones to update their CurrentOccupancy
        var fromZone = await _unitOfWork.Repository<Zone>().GetById(request.FromZoneId);
        var toZone = await _unitOfWork.Repository<Zone>().GetById(request.ToZoneId);

        if (fromZone is null)
        {
            return Error.NotFound(description: "Source zone not found.");
        }

        if (toZone is null)
        {
            return Error.NotFound(description: "Destination zone not found.");
        }

        // Update occupancy counts
        fromZone.CurrentOccupancy = Math.Max(0, fromZone.CurrentOccupancy - 1);
        toZone.CurrentOccupancy += 1;

        _unitOfWork.Repository<Zone>().Update(fromZone);
        _unitOfWork.Repository<Zone>().Update(toZone);

        var transition = await _unitOfWork.Repository<Transition>().Add(new Transition
        {
            DeviceId = request.DeviceId,
            FromZone = request.FromZoneId,
            ToZone = request.ToZoneId,
            CreatedAt = request.CreatedAt ?? DateTime.UtcNow,
        });

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Transition created: FromZone {FromZoneId} (occupancy: {FromOccupancy}) -> ToZone {ToZoneId} (occupancy: {ToOccupancy})",
            fromZone.Id, fromZone.CurrentOccupancy, toZone.Id, toZone.CurrentOccupancy);

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