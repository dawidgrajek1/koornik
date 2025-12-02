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
}
