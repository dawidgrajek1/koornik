using ErrorOr;
using Koornik.Application.Interfaces;
using Koornik.Application.Mapping;
using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Koornik.Domain.Specifications;
using Microsoft.Extensions.Logging;

namespace Koornik.Application.Services;

public class DeviceService(IUnitOfWork unitOfWork, ILogger<DeviceService> logger) : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<DeviceService> _logger = logger;

    public async Task<ErrorOr<DeviceResponse>> CreateDevice(CreateDeviceRequest request)
    {
        List<Error> errors = [];

        if (string.IsNullOrEmpty(request.Name))
        {
            errors.Add(Error.Validation(description: "Device name cannot be empty."));
        }

        if (string.IsNullOrEmpty(request.IpAddress))
        {
            errors.Add(Error.Validation(description: "IP address cannot be empty."));
        }
        else
        {
            var octets = request.IpAddress.Split('.').Select(s => short.TryParse(s, out var octet) ? octet : (short)-1).ToList();
            if (octets.Count != 4 || octets.Any(octet => octet is < 0 or > 255))
            {
                errors.Add(Error.Validation(description: "Invalid IP address format."));
            }
        }

        if (string.IsNullOrEmpty(request.MacAddress))
        {
            errors.Add(Error.Validation(description: "MAC address cannot be empty."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var device = await _unitOfWork.Repository<Device>().Add(new Device
        {
            Name = request.Name,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            FrontZoneId = request.FrontZoneId,
            BackZoneId = request.BackZoneId,
        });

        await _unitOfWork.SaveChangesAsync();
        return device.ToResponse();
    }

    public async Task<ErrorOr<IEnumerable<DeviceResponse>>> GetAllDevices()
    {
        var devices = await _unitOfWork.Repository<Device>().ListAll();
        return devices.Select(d => d.ToResponse()).ToList();
    }

    public async Task<ErrorOr<DeviceResponse>> GetDeviceById(Guid id)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation(description: "Device id should not be empty.");
        }

        var device = await _unitOfWork.Repository<Device>().GetById(id);

        if (device is null)
        {
            return Error.NotFound(description: "Device not found.");
        }

        return device.ToResponse();
    }

    public async Task<ErrorOr<DeviceResponse>> GetDeviceByMacAddress(string macAddress)
    {
        if (string.IsNullOrEmpty(macAddress))
        {
            return Error.Validation(description: "MAC address cannot be empty.");
        }
        
        var spec = DeviceSpecifications.GetByMacAddress(macAddress);
        var device = await _unitOfWork.Repository<Device>().FirstOrDefault(spec);
        
        if (device is null)
        {
            return Error.NotFound(description: "Device not found.");
        }
        
        return device.ToResponse();
    }

    public async Task<ErrorOr<DeviceResponse>> GetDeviceByZoneId(Guid zoneId)
    {
        if (zoneId == Guid.Empty)
        {
            return Error.Validation(description: "Zone id cannot be empty.");
        }
        
        var spec = DeviceSpecifications.GetByZoneId(zoneId);
        var device = await _unitOfWork.Repository<Device>().FirstOrDefault(spec);

        if (device is null)
        {
            return Error.NotFound(description: "Device not found.");
        }
        
        return device.ToResponse();
    }

    public async Task<ErrorOr<DeviceResponse>> UpdateDevice(Guid id, UpdateDeviceRequest request)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation(description: "Device id should not be empty.");
        }

        var device = await _unitOfWork.Repository<Device>().GetById(id);

        if (device is null)
        {
            return Error.NotFound(description: "Device not found.");
        }

        device.Name = request.Name;
        device.IpAddress = request.IpAddress;
        device.MacAddress = request.MacAddress;
        device.FrontZoneId = request.FrontZoneId;
        device.BackZoneId = request.BackZoneId;

        _unitOfWork.Repository<Device>().Update(device);
        await _unitOfWork.SaveChangesAsync();

        return device.ToResponse();
    }

    public async Task<ErrorOr<Deleted>> DeleteDevice(Guid deviceId)
    {
        if (deviceId == Guid.Empty)
        {
            return Result.Deleted;
        }

        var device = await _unitOfWork.Repository<Device>().GetById(deviceId);
        if (device is null)
        {
            return Result.Deleted;
        }

        _unitOfWork.Repository<Device>().Delete(device);
        await _unitOfWork.SaveChangesAsync();

        return Result.Deleted;
    }
}