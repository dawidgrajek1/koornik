using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using ErrorOr;

namespace Koornik.Application.Interfaces;

public interface IZoneService
{
    Task<ErrorOr<ZoneResponse>> CreateZone(CreateZoneRequest request);
    Task<ErrorOr<IEnumerable<ZoneResponse>>> GetAllZones();
    Task<ErrorOr<ZoneResponse>> GetZoneById(Guid id);
    Task<ErrorOr<ZoneResponse>> UpdateZone(Guid id, UpdateZoneRequest request);
    Task<ErrorOr<Deleted>> DeleteZone(Guid id);
}