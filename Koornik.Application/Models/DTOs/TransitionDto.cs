using Koornik.Domain.Entities;

namespace Koornik.Application.Models.DTOs;

public class TransitionDto(Transition log)
{
    public Guid Id { get; set; } = log.Id;
    public Guid DeviceId { get; set; } = log.DeviceId;
    public DateTime CreatedAt { get; set; } = log.CreatedAt;
    public Guid FromZoneId { get; set; } = log.FromZone;
    public Guid ToZoneId { get; set; } = log.ToZone;
}