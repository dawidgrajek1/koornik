using Koornik.Application.Models.DTOs;

namespace Koornik.Application.Models.Responses;

public class CreateZoneResponse
{
    public required ZoneDto Data { get; set; }
}