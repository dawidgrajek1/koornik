using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using ErrorOr;

namespace Koornik.Application.Interfaces;

public interface ITransitionService
{
    Task<ErrorOr<TransitionResponse>> CreateTransition(CreateTransitionRequest request);
    Task<ErrorOr<IEnumerable<TransitionResponse>>> GetAllTransitions();
    Task<ErrorOr<TransitionResponse>> GetTransitionById(Guid id);
    Task<ErrorOr<IEnumerable<TransitionResponse>>> GetTransitionsForZone(Guid zoneId, DateTime? start = null, DateTime? end = null);
    Task<ErrorOr<IEnumerable<TransitionResponse>>> GetTransitionsForDevice(Guid deviceId, DateTime? start = null, DateTime? end = null);
    Task<ErrorOr<IEnumerable<TransitionResponse>>> GetTransitionsForTimeRange(DateTime start, DateTime end);
}