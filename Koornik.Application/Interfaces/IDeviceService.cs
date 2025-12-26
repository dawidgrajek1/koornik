using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using ErrorOr;

namespace Koornik.Application.Interfaces;

public interface IDeviceService
{
    Task<ErrorOr<DeviceResponse>> CreateDevice(CreateDeviceRequest request);
    Task<ErrorOr<IEnumerable<DeviceResponse>>> GetAllDevices();
    Task<ErrorOr<DeviceResponse>> GetDeviceById(Guid id);
    Task<ErrorOr<DeviceResponse>> GetDeviceByMacAddress(string macAddress);
    Task<ErrorOr<DeviceResponse>> GetDeviceByZoneId(Guid zoneId);
    Task<ErrorOr<DeviceResponse>> UpdateDevice(Guid id, UpdateDeviceRequest request);
    Task<ErrorOr<Deleted>> DeleteDevice(Guid deviceId);
}