using Koornik.Application.Models.Requests;
using Koornik.Application.Models.Responses;

namespace Koornik.Application.Interfaces;

public interface IZoneService
{
    public Task<CreateZoneResponse> CreateZone(CreateZoneRequest request);
    public Task<GetAllZonesResponse> GetAllZones();
    
}