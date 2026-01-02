using Koornik.Domain.Core.Specifications;
using Koornik.Domain.Entities;

namespace Koornik.Domain.Specifications;

public static class ZoneStateSnapshotSpecifications
{
    /// <summary>
    /// Get the latest snapshot for a specific zone.
    /// </summary>
    public static BaseSpecification<ZoneStateSnapshot> GetLatestByZone(Guid zoneId)
    {
        var spec = new BaseSpecification<ZoneStateSnapshot>(s => s.ZoneId == zoneId);
        spec.ApplyOrderByDescending(s => s.CreatedAt);
        spec.ApplyPaging(0, 1);
        return spec;
    }

    /// <summary>
    /// Get the latest snapshot for a specific zone before a given timestamp.
    /// Used for calculating historical state at a point in time.
    /// </summary>
    public static BaseSpecification<ZoneStateSnapshot> GetLatestBeforeTimestamp(Guid zoneId, DateTime timestamp)
    {
        var spec = new BaseSpecification<ZoneStateSnapshot>(s => s.ZoneId == zoneId && s.CreatedAt <= timestamp);
        spec.ApplyOrderByDescending(s => s.CreatedAt);
        spec.ApplyPaging(0, 1);
        return spec;
    }

    /// <summary>
    /// Get all snapshots for a zone within a date range.
    /// </summary>
    public static BaseSpecification<ZoneStateSnapshot> GetByZoneAndDateRange(Guid zoneId, DateTime startDate, DateTime endDate)
    {
        var spec = new BaseSpecification<ZoneStateSnapshot>(
            s => s.ZoneId == zoneId && s.CreatedAt >= startDate && s.CreatedAt <= endDate);
        spec.ApplyOrderBy(s => s.CreatedAt);
        return spec;
    }

    /// <summary>
    /// Get all snapshots within a date range (for all zones).
    /// </summary>
    public static BaseSpecification<ZoneStateSnapshot> GetByDateRange(DateTime startDate, DateTime endDate)
    {
        var spec = new BaseSpecification<ZoneStateSnapshot>(
            s => s.CreatedAt >= startDate && s.CreatedAt <= endDate);
        spec.ApplyOrderBy(s => s.CreatedAt);
        return spec;
    }

    /// <summary>
    /// Check if any snapshot exists for a zone.
    /// </summary>
    public static BaseSpecification<ZoneStateSnapshot> GetAnyByZone(Guid zoneId)
    {
        var spec = new BaseSpecification<ZoneStateSnapshot>(s => s.ZoneId == zoneId);
        spec.ApplyPaging(0, 1);
        return spec;
    }
}
