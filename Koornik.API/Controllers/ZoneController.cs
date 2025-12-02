using Asp.Versioning;
using Koornik.API.Extensions;
using Koornik.Application.Interfaces;
using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Koornik.API.Controllers;

[ApiVersion("1")]
[Route("api/v{version:apiVersion}/zones")]
[ApiController]
public class ZoneController(IZoneService zoneService) : ControllerBase
{
    private readonly IZoneService _zoneService = zoneService;

    [HttpPost]
    [ProducesResponseType(typeof(ZoneResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateZone([FromBody] CreateZoneRequest request)
    {
        var result = await _zoneService.CreateZone(request);
        return result.ToCreatedActionResult(nameof(GetZoneById), r => new { id = r.Id });
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ZoneResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllZones()
    {
        var result = await _zoneService.GetAllZones();
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}", Name = nameof(GetZoneById))]
    [ProducesResponseType(typeof(ZoneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZoneById(Guid id)
    {
        var result = await _zoneService.GetZoneById(id);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ZoneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateZone(Guid id, [FromBody] UpdateZoneRequest request)
    {
        var result = await _zoneService.UpdateZone(id, request);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteZone(Guid id)
    {
        var result = await _zoneService.DeleteZone(id);
        return result.ToNoContentActionResult();
    }
}
