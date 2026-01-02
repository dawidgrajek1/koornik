using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Refit;

namespace Koornik.Contracts.Api;

public interface IZoneApi
{
    [Post("/api/v{version}/zones")]
    Task<ZoneResponse> CreateZone([Body] CreateZoneRequest request, string version = "1");

    [Get("/api/v{version}/zones")]
    Task<IEnumerable<ZoneResponse>> GetAllZones(string version = "1");

    [Get("/api/v{version}/zones/{id}")]
    Task<ZoneResponse> GetZoneById(Guid id, string version = "1");

    [Put("/api/v{version}/zones/{id}")]
    Task<ZoneResponse> UpdateZone(Guid id, [Body] UpdateZoneRequest request, string version = "1");

    [Delete("/api/v{version}/zones/{id}")]
    Task DeleteZone(Guid id, string version = "1");

    /// <summary>
    /// Get the current state of a zone.
    /// </summary>
    [Get("/api/v{version}/zones/{id}/state")]
    Task<ZoneStateResponse> GetZoneState(Guid id, string version = "1");

    /// <summary>
    /// Get the state of a zone at a specific point in time.
    /// </summary>
    [Get("/api/v{version}/zones/{id}/state/at")]
    Task<ZoneStateResponse> GetZoneStateAt(Guid id, DateTime timestamp, string version = "1");

    /// <summary>
    /// Get the state history of a zone within a time range.
    /// </summary>
    [Get("/api/v{version}/zones/{id}/state/history")]
    Task<ZoneStateHistoryResponse> GetZoneStateHistory(Guid id, DateTime startDate, DateTime endDate, string version = "1");
}
