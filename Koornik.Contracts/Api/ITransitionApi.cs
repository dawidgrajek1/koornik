using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Refit;

namespace Koornik.Contracts.Api;

public interface ITransitionApi
{
    [Post("/api/v{version}/transitions")]
    Task<TransitionResponse> CreateTransition([Body] CreateTransitionRequest request, string version = "1");

    [Get("/api/v{version}/transitions")]
    Task<IEnumerable<TransitionResponse>> GetAllTransitions(string version = "1");

    [Get("/api/v{version}/transitions/{id}")]
    Task<TransitionResponse> GetTransitionById(Guid id, string version = "1");

    [Get("/api/v{version}/transitions/by-zone/{zoneId}")]
    Task<IEnumerable<TransitionResponse>> GetTransitionsForZone(Guid zoneId, DateTime? start = null, DateTime? end = null, string version = "1");

    [Get("/api/v{version}/transitions/by-device/{deviceId}")]
    Task<IEnumerable<TransitionResponse>> GetTransitionsForDevice(Guid deviceId, DateTime? start = null, DateTime? end = null, string version = "1");

    [Get("/api/v{version}/transitions/by-time-range")]
    Task<IEnumerable<TransitionResponse>> GetTransitionsForTimeRange(DateTime start, DateTime end, string version = "1");
}
