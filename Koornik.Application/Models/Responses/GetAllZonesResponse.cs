using Koornik.Application.Models.DTOs;

namespace Koornik.Application.Models.Responses;

public class GetAllZonesResponse
{
    public required IEnumerable<ZoneDto> Data { get; set; }
}