namespace Koornik.Application.Interfaces;

/// <summary>
/// Service for sending real-time notifications to connected clients.
/// </summary>
public interface IRealTimeNotificationService
{
    /// <summary>
    /// Notifies all connected clients about a transition event.
    /// </summary>
    Task NotifyTransitionAsync(TransitionEvent transitionEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients about a device status update.
    /// </summary>
    Task NotifyDeviceStatusAsync(DeviceStatusEvent statusEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients about distance readings.
    /// </summary>
    Task NotifyDistanceReadingAsync(DistanceReadingEvent distanceEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients subscribed to a specific device about a transition event.
    /// </summary>
    Task NotifyDeviceTransitionAsync(string macAddress, TransitionEvent transitionEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients subscribed to a specific zone about events.
    /// </summary>
    Task NotifyZoneEventAsync(Guid zoneId, TransitionEvent transitionEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies all connected clients about a zone occupancy change.
    /// </summary>
    Task NotifyZoneOccupancyChangedAsync(ZoneOccupancyChangedEvent occupancyEvent, CancellationToken cancellationToken = default);
}
