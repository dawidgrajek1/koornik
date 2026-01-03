using System.Text.Json;
using Koornik.Application.Interfaces;
using Koornik.Contracts.Requests;
using Koornik.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koornik.Infrastructure.Mqtt;

/// <summary>
/// Handles incoming MQTT messages from IoT devices.
/// </summary>
public class MqttMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRealTimeNotificationService _notificationService;
    private readonly ILogger<MqttMessageHandler> _logger;
    private readonly MqttOptions _options;

    public MqttMessageHandler(
        IServiceScopeFactory scopeFactory,
        IRealTimeNotificationService notificationService,
        ILogger<MqttMessageHandler> logger,
        IOptions<MqttOptions> options)
    {
        _scopeFactory = scopeFactory;
        _notificationService = notificationService;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Processes an incoming MQTT message based on its topic.
    /// </summary>
    public async Task HandleMessageAsync(string topic, string payload)
    {
        try
        {
            // Parse topic: koornik/{mac}/transition, koornik/{mac}/status, koornik/{mac}/distance
            var segments = topic.Split('/');
            if (segments.Length != 3 || segments[0] != _options.TopicPrefix)
            {
                _logger.LogWarning("Received message on unexpected topic format: {Topic}", topic);
                return;
            }

            var macAddress = segments[1];
            var messageType = segments[2];

            switch (messageType)
            {
                case "transition":
                    await HandleTransitionMessage(macAddress, payload);
                    break;
                case "status":
                    await HandleStatusMessage(macAddress, payload);
                    break;
                case "distance":
                    await HandleDistanceMessage(macAddress, payload);
                    break;
                default:
                    _logger.LogWarning("Unknown message type: {MessageType} from {Mac}", messageType, macAddress);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT message on topic {Topic}", topic);
        }
    }

    private async Task HandleTransitionMessage(string macAddress, string payload)
    {
        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var transitionService = scope.ServiceProvider.GetRequiredService<ITransitionService>();
        var zoneService = scope.ServiceProvider.GetRequiredService<IZoneService>();

        // Parse the transition message
        TransitionPayload? transitionPayload;
        try
        {
            transitionPayload = JsonSerializer.Deserialize<TransitionPayload>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse transition payload: {Payload}", payload);
            return;
        }

        if (transitionPayload is null || string.IsNullOrEmpty(transitionPayload.Direction))
        {
            _logger.LogWarning("Invalid transition payload from {Mac}: {Payload}", macAddress, payload);
            return;
        }

        // Look up the device by MAC address
        var deviceResult = await deviceService.GetDeviceByMacAddress(macAddress);
        if (deviceResult.IsError)
        {
            _logger.LogWarning("Device not found for MAC {Mac}: {Error}", macAddress, deviceResult.FirstError.Description);
            return;
        }

        var device = deviceResult.Value;

        // Determine FromZone and ToZone based on direction
        // IN = movement from back to front (entering the front zone)
        // OUT = movement from front to back (entering the back zone)
        Guid fromZoneId, toZoneId;
        if (transitionPayload.Direction.Equals("IN", StringComparison.OrdinalIgnoreCase))
        {
            fromZoneId = device.BackZoneId;
            toZoneId = device.FrontZoneId;
        }
        else if (transitionPayload.Direction.Equals("OUT", StringComparison.OrdinalIgnoreCase))
        {
            fromZoneId = device.FrontZoneId;
            toZoneId = device.BackZoneId;
        }
        else
        {
            _logger.LogWarning("Unknown direction '{Direction}' from {Mac}", transitionPayload.Direction, macAddress);
            return;
        }

        // Create the transition with server-side timestamp
        var timestamp = DateTime.UtcNow;
        var request = new CreateTransitionRequest
        {
            DeviceId = device.Id,
            FromZoneId = fromZoneId,
            ToZoneId = toZoneId,
            CreatedAt = timestamp
        };

        var result = await transitionService.CreateTransition(request);
        if (result.IsError)
        {
            _logger.LogError("Failed to create transition: {Error}", result.FirstError.Description);
            return;
        }

        _logger.LogInformation(
            "Transition recorded: {Direction} on device {DeviceName} ({Mac}) from {FromZone} to {ToZone}",
            transitionPayload.Direction, device.Name, macAddress, fromZoneId, toZoneId);

        // Notify connected clients via SignalR
        var transitionEvent = new TransitionEvent
        {
            TransitionId = result.Value.Id,
            DeviceId = device.Id,
            DeviceName = device.Name,
            MacAddress = macAddress,
            Direction = transitionPayload.Direction.ToUpperInvariant(),
            FromZoneId = fromZoneId,
            ToZoneId = toZoneId,
            Timestamp = timestamp
        };

        // Broadcast to all clients and specific device/zone groups
        await _notificationService.NotifyTransitionAsync(transitionEvent);
        await _notificationService.NotifyDeviceTransitionAsync(macAddress, transitionEvent);
        await _notificationService.NotifyZoneEventAsync(fromZoneId, transitionEvent);
        await _notificationService.NotifyZoneEventAsync(toZoneId, transitionEvent);

        // Fetch updated zones and broadcast occupancy changes
        var fromZoneResult = await zoneService.GetZoneById(fromZoneId);
        var toZoneResult = await zoneService.GetZoneById(toZoneId);

        if (!fromZoneResult.IsError)
        {
            var fromZone = fromZoneResult.Value;
            await _notificationService.NotifyZoneOccupancyChangedAsync(new ZoneOccupancyChangedEvent
            {
                ZoneId = fromZone.Id,
                ZoneName = fromZone.Name,
                CurrentOccupancy = fromZone.CurrentOccupancy,
                Capacity = fromZone.Capacity,
                Timestamp = timestamp
            });
        }

        if (!toZoneResult.IsError)
        {
            var toZone = toZoneResult.Value;
            await _notificationService.NotifyZoneOccupancyChangedAsync(new ZoneOccupancyChangedEvent
            {
                ZoneId = toZone.Id,
                ZoneName = toZone.Name,
                CurrentOccupancy = toZone.CurrentOccupancy,
                Capacity = toZone.Capacity,
                Timestamp = timestamp
            });
        }
    }

    private async Task HandleStatusMessage(string macAddress, string payload)
    {
        try
        {
            var statusPayload = JsonSerializer.Deserialize<StatusPayload>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (statusPayload is not null)
            {
                _logger.LogInformation(
                    "Device {Mac} status: {State}{Thresholds}",
                    macAddress,
                    statusPayload.State,
                    statusPayload.Thresholds is not null
                        ? $" (thresholds: front={statusPayload.Thresholds.Front}, back={statusPayload.Thresholds.Back})"
                        : "");

                // Notify connected clients via SignalR
                var statusEvent = new DeviceStatusEvent
                {
                    MacAddress = macAddress,
                    State = statusPayload.State,
                    ThresholdFront = statusPayload.Thresholds?.Front,
                    ThresholdBack = statusPayload.Thresholds?.Back,
                    Timestamp = DateTime.UtcNow
                };

                await _notificationService.NotifyDeviceStatusAsync(statusEvent);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse status payload from {Mac}: {Payload}", macAddress, payload);
        }
    }

    private async Task HandleDistanceMessage(string macAddress, string payload)
    {
        try
        {
            var distancePayload = JsonSerializer.Deserialize<DistancePayload>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (distancePayload is not null)
            {
                _logger.LogDebug(
                    "Device {Mac} distance: front={Front}mm, back={Back}mm",
                    macAddress, distancePayload.Front, distancePayload.Back);

                // Notify subscribed clients via SignalR (only to device-specific group)
                var distanceEvent = new DistanceReadingEvent
                {
                    MacAddress = macAddress,
                    FrontDistance = distancePayload.Front,
                    BackDistance = distancePayload.Back,
                    Timestamp = DateTime.UtcNow
                };

                await _notificationService.NotifyDistanceReadingAsync(distanceEvent);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse distance payload from {Mac}: {Payload}", macAddress, payload);
        }
    }

    #region Payload DTOs

    private class TransitionPayload
    {
        public string Direction { get; set; } = string.Empty;
    }

    private class StatusPayload
    {
        public string State { get; set; } = string.Empty;
        public ThresholdValues? Thresholds { get; set; }
    }

    private class ThresholdValues
    {
        public int Front { get; set; }
        public int Back { get; set; }
    }

    private class DistancePayload
    {
        public int Front { get; set; }
        public int Back { get; set; }
    }

    #endregion
}
