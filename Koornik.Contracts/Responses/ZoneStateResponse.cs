namespace Koornik.Contracts.Responses;

/// <summary>
/// Represents the state of a zone at a specific point in time.
/// </summary>
public class ZoneStateResponse
{
    /// <summary>
    /// The unique identifier of the zone.
    /// </summary>
    public Guid ZoneId { get; set; }

    /// <summary>
    /// The name of the zone.
    /// </summary>
    public string ZoneName { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp when this state was recorded or calculated.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The number of people currently in the zone.
    /// </summary>
    public int OccupancyCount { get; set; }

    /// <summary>
    /// The maximum capacity of the zone.
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// The occupancy percentage (OccupancyCount / Capacity * 100).
    /// </summary>
    public double OccupancyPercentage => Capacity > 0 ? Math.Round((double)OccupancyCount / Capacity * 100, 2) : 0;
}
