using ErrorOr;
using Koornik.Application.Interfaces;
using Koornik.Application.Models.DTOs;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Serilog;

namespace Koornik.Application.Services;

public class ZoneService(IUnitOfWork unitOfWork, ILogger logger) : IZoneService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger _logger = logger;

    public async Task<ErrorOr<ZoneDto>> CreateZone(ZoneDto newZone)
    {
        List<Error> errors = [];
        
        if (string.IsNullOrEmpty(newZone.Name))
        {
            errors.Add(Error.Validation(description: "Zone name should not be empty."));
        }

        if (newZone.Capacity < 1)
        {
            errors.Add(Error.Validation(description: "Zone capacity should not be less than 1."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }
        
        var zone = await _unitOfWork.Repository<Zone>().Add(new Zone
        {
            Name = newZone.Name,
            Capacity = newZone.Capacity,
        });
        
        
        await _unitOfWork.SaveChangesAsync();

        return new ZoneDto(zone);
    }
    
    public async Task<ErrorOr<ZoneDto>> GetZoneById(Guid id)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }
        
        var zone = await _unitOfWork.Repository<Zone>().GetById(id);

        if (zone is null)
        {
            return Error.NotFound(description: "Zone not found.");
        }
        
        return new ZoneDto(zone);
    }

    public async Task<ErrorOr<ZoneDto>> UpdateZone(ZoneDto newZone)
    {
        if (newZone.Id == Guid.Empty)
        {
            return Error.Validation(description: "Zone id should not be empty.");
        }
        
        var zone = await _unitOfWork.Repository<Zone>().GetById(newZone.Id);

        if (zone is null)
        {
            return Error.NotFound(description: "Zone not found.");
        }
        
        await _unitOfWork.Repository<Zone>().Update(zone);
        await _unitOfWork.SaveChangesAsync();

        return new ZoneDto(zone);
    }

    public async Task<ErrorOr<IEnumerable<ZoneDto>>> GetAllZones()
    {
        var zones = await _unitOfWork.Repository<Zone>().ListAll();
        return zones.Select(x => new ZoneDto(x)).ToList();
    }
    
    public async Task DeleteZone(Guid id)
    {
        await _unitOfWork.Repository<Zone>().Delete(id);
        await _unitOfWork.SaveChangesAsync();
    }
}