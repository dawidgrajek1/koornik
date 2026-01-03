namespace Koornik.Application.Interfaces;

/// <summary>
/// Defines the SignalR hub client methods that can be invoked on connected clients.
/// </summary>
public interface IKoornikHubClient
{
    /// <summary>
    /// Called when a transition event is detected (person moving between zones).
    /// </summary>
    Task OnTransition(TransitionEvent transitionEvent);

    /// <summary>
    /// Called when a device status update is received.
    /// </summary>
    Task OnDeviceStatus(DeviceStatusEvent statusEvent);

    /// <summary>
    /// Called when distance readings are received from a device.
    /// </summary>
    Task OnDistanceReading(DistanceReadingEvent distanceEvent);

    /// <summary>
    /// Called when a zone's occupancy changes.
    /// </summary>
    Task OnZoneOccupancyChanged(ZoneOccupancyChangedEvent occupancyEvent);
}

/// <summary>
/// Event raised when a transition is detected.
/// </summary>
public record TransitionEvent
{
    public Guid TransitionId { get; init; }
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public Guid FromZoneId { get; init; }
    public Guid ToZoneId { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Event raised when a device status update is received.
/// </summary>
public record DeviceStatusEvent
{
    public string MacAddress { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public int? ThresholdFront { get; init; }
    public int? ThresholdBack { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Event raised when distance readings are received.
/// </summary>
 public record DistanceReadingEvent
{
    public string MacAddress { get; init; } = string.Empty;
    public int FrontDistance { get; init; }
    public int BackDistance { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Event raised when a zone's occupancy changes.
/// </summary>
public record ZoneOccupancyChangedEvent
{
    public Guid ZoneId { get; init; }
    public string ZoneName { get; init; } = string.Empty;
    public int CurrentOccupancy { get; init; }
    public int Capacity { get; init; }
    public DateTime Timestamp { get; init; }
}
