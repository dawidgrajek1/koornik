using Asp.Versioning;
using Koornik.API.Extensions;
using Koornik.Application.Interfaces;
using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Koornik.API.Controllers;

[ApiVersion("1")]
[Route("api/v{version:apiVersion}/transitions")]
[ApiController]
public class TransitionController(ITransitionService transitionService) : ControllerBase
{
    private readonly ITransitionService _transitionService = transitionService;

    [HttpPost]
    [ProducesResponseType(typeof(TransitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransition([FromBody] CreateTransitionRequest request)
    {
        var result = await _transitionService.CreateTransition(request);
        return result.ToCreatedActionResult(nameof(GetTransitionById), r => new { id = r.Id });
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TransitionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllTransitions()
    {
        var result = await _transitionService.GetAllTransitions();
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}", Name = nameof(GetTransitionById))]
    [ProducesResponseType(typeof(TransitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransitionById(Guid id)
    {
        var result = await _transitionService.GetTransitionById(id);
        return result.ToActionResult();
    }

    [HttpGet("by-zone/{zoneId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<TransitionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTransitionsForZone(Guid zoneId, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
    {
        var result = await _transitionService.GetTransitionsForZone(zoneId, start, end);
        return result.ToActionResult();
    }

    [HttpGet("by-device/{deviceId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<TransitionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTransitionsForDevice(Guid deviceId, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
    {
        var result = await _transitionService.GetTransitionsForDevice(deviceId, start, end);
        return result.ToActionResult();
    }

    [HttpGet("by-time-range")]
    [ProducesResponseType(typeof(IEnumerable<TransitionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransitionsForTimeRange([FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        var result = await _transitionService.GetTransitionsForTimeRange(start, end);
        return result.ToActionResult();
    }
}
