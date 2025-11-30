using ErrorOr;
using Koornik.Application.Interfaces;
using Koornik.Application.Models.DTOs;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Koornik.Domain.Specifications;
using Serilog;

namespace Koornik.Application.Services;

public class TransitionService(IUnitOfWork unitOfWork, ILogger logger) : ITransitionService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger _logger = logger;
    
    public async Task<ErrorOr<TransitionDto>> CreateTransition(TransitionDto newTransition)
    {
        List<Error> errors = [];
        
        if (newTransition.DeviceId == Guid.Empty)
        {
            errors.Add(Error.Validation(description: "Device id should not be empty."));
        }
        
        if (newTransition.FromZoneId == Guid.Empty)
        {
            errors.Add(Error.Validation(description: "Source zone id should not be empty."));
        }
        
        if (newTransition.ToZoneId == Guid.Empty)
        {
            errors.Add(Error.Validation(description: "Destination zone id should not be empty."));
        }
        
        if (errors.Count > 0)
        {
            return errors;
        }
        
        var transition = await _unitOfWork.Repository<Transition>().Add(new Transition
        {
            DeviceId = newTransition.DeviceId,
            FromZone = newTransition.FromZoneId,
            ToZone = newTransition.ToZoneId,
            CreatedAt = newTransition.CreatedAt,
        });
        
        await _unitOfWork.SaveChangesAsync();
        return new TransitionDto(transition);
    }

    public async Task<ErrorOr<IEnumerable<TransitionDto>>> GetAllTransitions()
    {
        var transitions = await _unitOfWork.Repository<Transition>().ListAll();
        return transitions.Select(x => new TransitionDto(x)).ToList();
    }

    public async Task<ErrorOr<TransitionDto>> GetTransitionById(Guid id)
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
        
        return new TransitionDto(transition);
    }

    public async Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForZone(Guid zoneId)
    {
        if (zoneId == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }

        var spec = TransitionSpecifications.GetByZoneId(zoneId);
        var transitions = await _unitOfWork.Repository<Transition>().List(spec);
        
        return transitions.Select(x => new TransitionDto(x)).ToList();
    }

    public async Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForZone(Guid zoneId, DateTime start, DateTime end)
    {
        if (zoneId == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }
        var spec = TransitionSpecifications.GetByZoneAndDateRange(zoneId, start, end);
        var transitions = await _unitOfWork.Repository<Transition>().List(spec);
        
        return transitions.Select(x => new TransitionDto(x)).ToList();
    }

    public async Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForDevice(Guid deviceId)
    {
        if (deviceId == Guid.Empty)
        {
            return Error.Validation(description: "Device id should not be empty.");
        }
        
        var spec = TransitionSpecifications.GetByDeviceId(deviceId);
        var transitions = await _unitOfWork.Repository<Transition>().List(spec);
        
        return transitions.Select(x => new TransitionDto(x)).ToList();
    }

    public async Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForDevice(Guid deviceId, DateTime start, DateTime end)
    {
        if (deviceId == Guid.Empty)
        {
            return Error.Validation(description: "Device id should not be empty.");
        }
        
        var spec = TransitionSpecifications.GetByDeviceAndDateRange(deviceId, start, end);
        var transitions = await _unitOfWork.Repository<Transition>().List(spec);
        
        return transitions.Select(x => new TransitionDto(x)).ToList();
    }

    public async Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForTimeRange(DateTime start, DateTime end)
    {
        var spec = TransitionSpecifications.GetByDateRange(start, end);
        var transitions = await _unitOfWork.Repository<Transition>().List(spec);
        
        return transitions.Select(x => new TransitionDto(x)).ToList();
    }
}