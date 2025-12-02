using Asp.Versioning;
using Koornik.API.Extensions;
using Koornik.Application.Interfaces;
using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Koornik.API.Controllers;

[ApiVersion("1")]
[Route("api/v{version:apiVersion}/devices")]
[ApiController]
public class DeviceController(IDeviceService deviceService) : ControllerBase
{
    private readonly IDeviceService _deviceService = deviceService;

    [HttpPost]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest request)
    {
        var result = await _deviceService.CreateDevice(request);
        return result.ToCreatedActionResult(nameof(GetDeviceById), r => new { id = r.Id });
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeviceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllDevices()
    {
        var result = await _deviceService.GetAllDevices();
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}", Name = nameof(GetDeviceById))]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeviceById(Guid id)
    {
        var result = await _deviceService.GetDeviceById(id);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDevice(Guid id, [FromBody] UpdateDeviceRequest request)
    {
        var result = await _deviceService.UpdateDevice(id, request);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteDevice(Guid id)
    {
        var result = await _deviceService.DeleteDevice(id);
        return result.ToNoContentActionResult();
    }
}