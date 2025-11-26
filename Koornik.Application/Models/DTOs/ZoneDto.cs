using Koornik.Domain.Entities;

namespace Koornik.Application.Models.DTOs;

public class ZoneDto(Zone zone)
{
    public Guid Id { get; set; } = zone.Id;
    public string Name { get; set; } = zone.Name;
    public int Capacity { get; set; } = zone.Capacity;
}