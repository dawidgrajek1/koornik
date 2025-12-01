using ErrorOr;
using Koornik.Application.Models.DTOs;

namespace Koornik.Application.Interfaces;

public interface IZoneService
{
    public Task<ErrorOr<ZoneDto>> CreateZone(ZoneDto newZone);
    public Task<ErrorOr<IEnumerable<ZoneDto>>> GetAllZones();
    public Task <ErrorOr<ZoneDto>> GetZoneById(Guid id);
    public Task <ErrorOr<ZoneDto>> UpdateZone(ZoneDto newZone);
    public Task DeleteZone(Guid id);
}