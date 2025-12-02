using ErrorOr;
using Koornik.Application.Interfaces;
using Koornik.Application.Mapping;
using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Koornik.Application.Services;

public class ZoneService(IUnitOfWork unitOfWork, ILogger<ZoneService> logger) : IZoneService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ZoneService> _logger = logger;

    public async Task<ErrorOr<ZoneResponse>> CreateZone(CreateZoneRequest request)
    {
        List<Error> errors = [];

        if (string.IsNullOrEmpty(request.Name))
        {
            errors.Add(Error.Validation(description: "Zone name should not be empty."));
        }

        if (request.Capacity < 1)
        {
            errors.Add(Error.Validation(description: "Zone capacity should not be less than 1."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var zone = await _unitOfWork.Repository<Zone>().Add(new Zone
        {
            Name = request.Name,
            Capacity = request.Capacity,
        });

        await _unitOfWork.SaveChangesAsync();
        return zone.ToResponse();
    }

    public async Task<ErrorOr<ZoneResponse>> GetZoneById(Guid id)
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

        return zone.ToResponse();
    }

    public async Task<ErrorOr<ZoneResponse>> UpdateZone(Guid id, UpdateZoneRequest request)
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

        zone.Name = request.Name;
        zone.Capacity = request.Capacity;

        _unitOfWork.Repository<Zone>().Update(zone);
        await _unitOfWork.SaveChangesAsync();

        return zone.ToResponse();
    }

    public async Task<ErrorOr<IEnumerable<ZoneResponse>>> GetAllZones()
    {
        var zones = await _unitOfWork.Repository<Zone>().ListAll();
        return zones.Select(x => x.ToResponse()).ToList();
    }

    public async Task<ErrorOr<Deleted>> DeleteZone(Guid id)
    {
        var zone = await _unitOfWork.Repository<Zone>().GetById(id);
        if (zone is null)
        {
            return Result.Deleted;
        }

        _unitOfWork.Repository<Zone>().Delete(zone);
        await _unitOfWork.SaveChangesAsync();

        return Result.Deleted;
    }
}