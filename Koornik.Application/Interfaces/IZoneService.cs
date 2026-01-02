using Koornik.Contracts.Requests;
using Koornik.Contracts.Responses;
using ErrorOr;

namespace Koornik.Application.Interfaces;

public interface IZoneService
{
    Task<ErrorOr<ZoneResponse>> CreateZone(CreateZoneRequest request);
    Task<ErrorOr<IEnumerable<ZoneResponse>>> GetAllZones();
    Task<ErrorOr<ZoneResponse>> GetZoneById(Guid id);
    Task<ErrorOr<ZoneResponse>> UpdateZone(Guid id, UpdateZoneRequest request);
    Task<ErrorOr<Deleted>> DeleteZone(Guid id);
    
    /// <summary>
    /// Get the current state of a zone (latest snapshot + transitions since).
    /// </summary>
    Task<ErrorOr<ZoneStateResponse>> GetCurrentState(Guid zoneId);
    
    /// <summary>
    /// Get the state of a zone at a specific point in time.
    /// </summary>
    Task<ErrorOr<ZoneStateResponse>> GetStateAt(Guid zoneId, DateTime timestamp);
    
    /// <summary>
    /// Get the state history of a zone within a time range.
    /// </summary>
    Task<ErrorOr<ZoneStateHistoryResponse>> GetStateHistory(Guid zoneId, DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// Calculate the current occupancy count for a zone based on snapshot and transitions.
    /// Used internally and by the snapshot service.
    /// </summary>
    Task<int> CalculateCurrentOccupancy(Guid zoneId);
}