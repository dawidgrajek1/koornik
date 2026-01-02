using Koornik.Application.Interfaces;
using Koornik.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Koornik.Infrastructure.Services;

/// <summary>
/// Service for sending real-time notifications via SignalR.
/// </summary>
public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly IHubContext<KoornikHub, IKoornikHubClient> _hubContext;
    private readonly ILogger<RealTimeNotificationService> _logger;

    public RealTimeNotificationService(
        IHubContext<KoornikHub, IKoornikHubClient> hubContext,
        ILogger<RealTimeNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyTransitionAsync(TransitionEvent transitionEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.OnTransition(transitionEvent);
            _logger.LogDebug("Broadcast transition event for device {MacAddress}", transitionEvent.MacAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast transition event");
        }
    }

    public async Task NotifyDeviceStatusAsync(DeviceStatusEvent statusEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.OnDeviceStatus(statusEvent);
            _logger.LogDebug("Broadcast device status event for {MacAddress}", statusEvent.MacAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast device status event");
        }
    }

    public async Task NotifyDistanceReadingAsync(DistanceReadingEvent distanceEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Distance readings are high-frequency, only send to device subscribers
            var groupName = KoornikHub.GetDeviceGroupName(distanceEvent.MacAddress);
            await _hubContext.Clients.Group(groupName).OnDistanceReading(distanceEvent);
            _logger.LogTrace("Sent distance reading to group {Group}", groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send distance reading event");
        }
    }

    public async Task NotifyDeviceTransitionAsync(string macAddress, TransitionEvent transitionEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = KoornikHub.GetDeviceGroupName(macAddress);
            await _hubContext.Clients.Group(groupName).OnTransition(transitionEvent);
            _logger.LogDebug("Sent transition event to device group {Group}", groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send device transition event");
        }
    }

    public async Task NotifyZoneEventAsync(Guid zoneId, TransitionEvent transitionEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = KoornikHub.GetZoneGroupName(zoneId);
            await _hubContext.Clients.Group(groupName).OnTransition(transitionEvent);
            _logger.LogDebug("Sent transition event to zone group {Group}", groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send zone transition event");
        }
    }
}
