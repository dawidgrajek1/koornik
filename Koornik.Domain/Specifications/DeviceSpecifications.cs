using Koornik.Domain.Core.Specifications;
using Koornik.Domain.Entities;

namespace Koornik.Domain.Specifications;

public static class DeviceSpecifications
{
    public static BaseSpecification<Device> GetById(Guid id)
    {
        return new BaseSpecification<Device>(t => t.Id == id);
    }

    public static BaseSpecification<Device> GetByMacAddress(string mac)
    {
        return new BaseSpecification<Device>(t => t.MacAddress == mac);
    }

    public static BaseSpecification<Device> GetByZoneId(Guid zoneId)
    {
        return new BaseSpecification<Device>(t => t.BackZoneId == zoneId || t.FrontZoneId == zoneId);
    }
}