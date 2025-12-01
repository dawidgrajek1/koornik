using ErrorOr;
using Koornik.Application.Models.DTOs;

namespace Koornik.Application.Interfaces;

public interface ITransitionService
{
    public Task<ErrorOr<TransitionDto>> CreateTransition(TransitionDto newTransition);
    public Task<ErrorOr<IEnumerable<TransitionDto>>> GetAllTransitions();
    public Task<ErrorOr<TransitionDto>> GetTransitionById(Guid id);
    public Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForZone(Guid zoneId);
    public Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForZone(Guid zoneId, DateTime start, DateTime end);
    public Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForDevice(Guid deviceId);
    public Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForDevice(Guid deviceId, DateTime start, DateTime end);
    public Task<ErrorOr<IEnumerable<TransitionDto>>> GetTransitionsForTimeRange(DateTime start, DateTime end);
}