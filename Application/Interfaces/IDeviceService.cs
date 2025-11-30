using Koornik.Application.Models.DTOs;
using ErrorOr;

namespace Koornik.Application.Interfaces;

public interface IDeviceService
{
    public Task<ErrorOr<DeviceDto>> CreateDevice(DeviceDto newDevice);
    public Task<ErrorOr<IEnumerable<DeviceDto>>> GetAllDevices();
    public Task <ErrorOr<DeviceDto>> GetDeviceById(Guid id);
    public Task <ErrorOr<DeviceDto>> UpdateDevice(DeviceDto newDevice);
    public Task DeleteDevice(Guid id);
}