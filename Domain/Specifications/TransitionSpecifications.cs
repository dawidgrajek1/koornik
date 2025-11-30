using Koornik.Domain.Core.Specifications;
using Koornik.Domain.Entities;

namespace Koornik.Domain.Specifications;

public static class TransitionSpecifications
{
    public static BaseSpecification<Transition> GetByDeviceId(Guid deviceId)
    {
        return new BaseSpecification<Transition>(t => t.DeviceId == deviceId);
    }
    
    public static BaseSpecification<Transition> GetByZoneId(Guid zoneId)
    {
        return new BaseSpecification<Transition>(t => t.FromZone == zoneId || t.ToZone == zoneId);
    }
    
    public static BaseSpecification<Transition> GetByDateRange(DateTime startDate, DateTime endDate)
    {
        return new BaseSpecification<Transition>(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate);
    }
    
    public static BaseSpecification<Transition> GetByDeviceAndDateRange(Guid deviceId, DateTime startDate, DateTime endDate)
    {
        return new BaseSpecification<Transition>(t => t.DeviceId == deviceId && t.CreatedAt >= startDate && t.CreatedAt <= endDate);
    }
    
    public static BaseSpecification<Transition> GetByZoneAndDateRange(Guid zoneId, DateTime startDate, DateTime endDate)
    {
        return new BaseSpecification<Transition>(t => (t.FromZone == zoneId || t.ToZone == zoneId) && t.CreatedAt >= startDate && t.CreatedAt <= endDate);
    }
    
    public static BaseSpecification<Transition> GetByDeviceZoneAndDateRange(Guid deviceId, Guid zoneId, DateTime startDate, DateTime endDate)
    {
        return new BaseSpecification<Transition>(t => t.DeviceId == deviceId && (t.FromZone == zoneId || t.ToZone == zoneId) && t.CreatedAt >= startDate && t.CreatedAt <= endDate);
    }
}