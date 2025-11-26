using Koornik.Application.Interfaces;
using Koornik.Application.Models.DTOs;
using Koornik.Application.Models.Requests;
using Koornik.Application.Models.Responses;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Serilog;

namespace Koornik.Application.Services;

public class ZoneService(IUnitOfWork unitOfWork, ILogger logger) : IZoneService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger _logger = logger;

    public async Task<CreateZoneResponse> CreateZone(CreateZoneRequest request)
    {
        var zone = await _unitOfWork.Repository<Zone>().Add(new Zone
        {
            Name = request.Name,
            Capacity = request.Capacity,
        });
        
        await _unitOfWork.SaveChangesAsync();
        
        return new CreateZoneResponse
        {
            Data = new ZoneDto(zone),
        };
    }
    
    public async Task<ZoneDto?> GetZoneById(Guid id)
    {
        var zone = await _unitOfWork.Repository<Zone>().GetById(id);

        return zone is not null ? new ZoneDto(zone) : null;
    }
    
    public async Task<GetAllZonesResponse> GetAllZones()
    {
        var zones = await _unitOfWork.Repository<Zone>().ListAll();
        
        return new GetAllZonesResponse
        {
            Data = zones.Select(x => new ZoneDto(x)).ToList(),
        };
    }
}