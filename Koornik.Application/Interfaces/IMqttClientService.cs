namespace Koornik.Application.Interfaces;

/// <summary>
/// Service for MQTT communication with IoT devices.
/// </summary>
public interface IMqttClientService
{
    /// <summary>
    /// Publishes a message to the specified MQTT topic.
    /// </summary>
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a calibrate command to the device with the specified MAC address.
    /// </summary>
    Task SendCalibrateCommand(string macAddress, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a settings update command to the device with the specified MAC address.
    /// </summary>
    Task SendSettingsUpdate(string macAddress, DeviceSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enables or disables live distance streaming for a device.
    /// </summary>
    Task SendEnableDistanceStream(string macAddress, bool enabled, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets whether the MQTT client is currently connected.
    /// </summary>
    bool IsConnected { get; }
}

/// <summary>
/// Settings that can be sent to a device.
/// </summary>
public class DeviceSettings
{
    public int? ThresholdFront { get; set; }
    public int? ThresholdBack { get; set; }
    public int? DetectionTimeoutMs { get; set; }
    public int? CooldownMs { get; set; }
}
