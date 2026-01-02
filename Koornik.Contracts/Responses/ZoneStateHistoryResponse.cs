namespace Koornik.Contracts.Responses;

/// <summary>
/// Represents a collection of zone state snapshots over a time period.
/// </summary>
public class ZoneStateHistoryResponse
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
    /// The start of the queried time range.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// The end of the queried time range.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// The list of state snapshots within the time range.
    /// </summary>
    public List<ZoneStateResponse> States { get; set; } = [];
}
