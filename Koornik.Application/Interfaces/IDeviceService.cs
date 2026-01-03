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
    
    /// <summary>
    /// Sends a calibration command to the device via MQTT.
    /// </summary>
    Task<ErrorOr<Success>> CalibrateDevice(Guid deviceId);
    
    /// <summary>
    /// Enables or disables distance streaming for the device via MQTT.
    /// </summary>
    Task<ErrorOr<Success>> SetDistanceStream(Guid deviceId, bool enabled);
}