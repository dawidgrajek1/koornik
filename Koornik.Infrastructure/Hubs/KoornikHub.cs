using Koornik.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Koornik.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time IoT device events.
/// Clients can subscribe to all events or filter by device/zone.
/// </summary>
public class KoornikHub : Hub<IKoornikHubClient>
{
    private readonly ILogger<KoornikHub> _logger;

    public KoornikHub(ILogger<KoornikHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to events from a specific device by MAC address.
    /// </summary>
    public async Task SubscribeToDevice(string macAddress)
    {
        var groupName = GetDeviceGroupName(macAddress);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} subscribed to device {MacAddress}", Context.ConnectionId, macAddress);
    }

    /// <summary>
    /// Unsubscribe from events from a specific device.
    /// </summary>
    public async Task UnsubscribeFromDevice(string macAddress)
    {
        var groupName = GetDeviceGroupName(macAddress);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from device {MacAddress}", Context.ConnectionId, macAddress);
    }

    /// <summary>
    /// Subscribe to events for a specific zone.
    /// </summary>
    public async Task SubscribeToZone(Guid zoneId)
    {
        var groupName = GetZoneGroupName(zoneId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} subscribed to zone {ZoneId}", Context.ConnectionId, zoneId);
    }

    /// <summary>
    /// Unsubscribe from events for a specific zone.
    /// </summary>
    public async Task UnsubscribeFromZone(Guid zoneId)
    {
        var groupName = GetZoneGroupName(zoneId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from zone {ZoneId}", Context.ConnectionId, zoneId);
    }

    internal static string GetDeviceGroupName(string macAddress) => $"device:{macAddress}";
    internal static string GetZoneGroupName(Guid zoneId) => $"zone:{zoneId}";
}
