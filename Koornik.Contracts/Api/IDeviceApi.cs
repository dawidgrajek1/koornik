using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Refit;

namespace Koornik.Contracts.Api;

public interface IDeviceApi
{
    [Post("/api/v{version}/devices")]
    Task<DeviceResponse> CreateDevice([Body] CreateDeviceRequest request, string version = "1");

    [Get("/api/v{version}/devices")]
    Task<IEnumerable<DeviceResponse>> GetAllDevices(string version = "1");

    [Get("/api/v{version}/devices/{id}")]
    Task<DeviceResponse> GetDeviceById(Guid id, string version = "1");

    [Put("/api/v{version}/devices/{id}")]
    Task<DeviceResponse> UpdateDevice(Guid id, [Body] UpdateDeviceRequest request, string version = "1");

    [Delete("/api/v{version}/devices/{id}")]
    Task DeleteDevice(Guid id, string version = "1");
}
