using ErrorOr;
using Koornik.Application.Interfaces;
using Koornik.Application.Models.DTOs;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Koornik.Application.Services;

public class DeviceService(IUnitOfWork unitOfWork, ILogger logger) : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger _logger = logger;
    
    public async Task<ErrorOr<DeviceDto>> CreateDevice(DeviceDto newDevice)
    {
        List<Error> errors = [];
        
        if (string.IsNullOrEmpty(newDevice.Name))
        {
            errors.Add(Error.Validation(description: "Device name cannot be empty."));
        }

        if (string.IsNullOrEmpty(newDevice.IpAddress))
        {
            errors.Add(Error.Validation(description: "IP address cannot be empty."));
        }
        
        var octets = newDevice.IpAddress.Split('.').Select(s => short.TryParse(s, out var octet) ? octet : (short)-1).ToList();
        if (octets.Count != 4 || octets.Any(octet => octet is < 0 or > 255))
        {
            errors.Add(Error.Validation(description: "Invalid IP address format."));
        }
        
        if (errors.Count > 0)
        {
            return errors;
        }
        
        var device = await _unitOfWork.Repository<Device>().Add(new Device
        {
            Name = newDevice.Name,
            IpAddress = newDevice.IpAddress,
            FrontZoneId =  newDevice.FrontZoneId,
            BackZoneId = newDevice.BackZoneId,
        });
        
        await _unitOfWork.SaveChangesAsync();
        return new DeviceDto(device);
    }

    public async Task<ErrorOr<IEnumerable<DeviceDto>>> GetAllDevices()
    {
        var devices = await _unitOfWork.Repository<Device>().ListAll();
        return devices.Select(d => new DeviceDto(d)).ToList();
    }

    public async Task<ErrorOr<DeviceDto>> GetDeviceById(Guid id)
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
        
        return new DeviceDto(device);
    }

    public async Task<ErrorOr<DeviceDto>> UpdateDevice(DeviceDto newDevice)
    {
        if (newDevice.Id == Guid.Empty)
        {
            return Error.Validation(description: "Device id should not be empty.");
        }
        
        var device = await _unitOfWork.Repository<Device>().GetById(newDevice.Id);

        if (device is null)
        {
            return Error.NotFound(description: "Device not found.");
        }

        device.Name = newDevice.Name;
        device.IpAddress = newDevice.IpAddress;
        device.FrontZoneId = newDevice.FrontZoneId;
        device.BackZoneId = newDevice.BackZoneId;
        
        _unitOfWork.Repository<Device>().Update(device);
        await _unitOfWork.SaveChangesAsync();

        return new DeviceDto(device);
    }

    public async Task DeleteDevice(Guid deviceId)
    {
        if (deviceId == Guid.Empty)
        {
            return;
        }
        
        var device = await _unitOfWork.Repository<Device>().GetById(deviceId);

        if (device is null)
        {
            return;
        }

        _unitOfWork.Repository<Device>().Delete(device);
        await _unitOfWork.SaveChangesAsync();
    }
}