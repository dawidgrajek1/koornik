using System.ComponentModel.DataAnnotations;
using Koornik.Domain.Core.Models;

namespace Koornik.Domain.Entities;

/// <summary>
/// Represents a point-in-time snapshot of a zone's occupancy state.
/// Snapshots are created periodically (hourly) and used as anchors for calculating
/// zone state at any given timestamp.
/// </summary>
public class ZoneStateSnapshot : BaseEntity, IBaseEventLog
{
    [Key]
    public Guid Id { get; set; }
    
    /// <summary>
    /// The zone this snapshot belongs to.
    /// </summary>
    public Guid ZoneId { get; set; }
    
    /// <summary>
    /// The timestamp when this snapshot was recorded.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The occupancy count at the time of the snapshot.
    /// </summary>
    public int OccupancyCount { get; set; }
    
    /// <summary>
    /// The zone's capacity at the time of the snapshot (denormalized for historical accuracy).
    /// </summary>
    public int Capacity { get; set; }
}
