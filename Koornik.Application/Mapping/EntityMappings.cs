using Koornik.Contracts.Responses;
using Koornik.Domain.Entities;

namespace Koornik.Application.Mapping;

public static class EntityMappings
{
    public static DeviceResponse ToResponse(this Device device) => new()
    {
        Id = device.Id,
        Name = device.Name,
        IpAddress = device.IpAddress,
        FrontZoneId = device.FrontZoneId,
        BackZoneId = device.BackZoneId
    };

    public static ZoneResponse ToResponse(this Zone zone) => new()
    {
        Id = zone.Id,
        Name = zone.Name,
        Capacity = zone.Capacity
    };

    public static TransitionResponse ToResponse(this Transition transition) => new()
    {
        Id = transition.Id,
        DeviceId = transition.DeviceId,
        CreatedAt = transition.CreatedAt,
        FromZoneId = transition.FromZone,
        ToZoneId = transition.ToZone
    };
}
