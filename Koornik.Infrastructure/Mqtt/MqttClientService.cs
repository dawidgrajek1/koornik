using System.Text;
using System.Text.Json;
using Koornik.Application.Interfaces;
using Koornik.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace Koornik.Infrastructure.Mqtt;

/// <summary>
/// Background service that maintains an MQTT connection and processes messages from IoT devices.
/// </summary>
public class MqttClientService : BackgroundService, IMqttClientService
{
    private readonly MqttOptions _options;
    private readonly MqttMessageHandler _messageHandler;
    private readonly ILogger<MqttClientService> _logger;
    private IMqttClient? _mqttClient;

    public MqttClientService(
        IOptions<MqttOptions> options,
        MqttMessageHandler messageHandler,
        ILogger<MqttClientService> logger)
    {
        _options = options.Value;
        _messageHandler = messageHandler;
        _logger = logger;
    }

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MQTT Client Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndSubscribeAsync(stoppingToken);
                
                // Keep running while connected
                while (_mqttClient?.IsConnected == true && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("MQTT connection lost. Reconnecting in {Delay} seconds...", _options.ReconnectDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MQTT client. Reconnecting in {Delay} seconds...", _options.ReconnectDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
            }
        }

        await DisconnectAsync();
        _logger.LogInformation("MQTT Client Service stopped.");
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        // Configure message handler
        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();
            
            _logger.LogDebug("Received MQTT message on {Topic}: {Payload}", topic, payload);
            
            await _messageHandler.HandleMessageAsync(topic, payload ?? string.Empty);
        };

        _mqttClient.DisconnectedAsync += e =>
        {
            if (e.Exception is not null)
            {
                _logger.LogWarning(e.Exception, "MQTT client disconnected unexpectedly");
            }
            else
            {
                _logger.LogInformation("MQTT client disconnected");
            }
            return Task.CompletedTask;
        };

        // Build connection options
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_options.Username))
        {
            optionsBuilder.WithCredentials(_options.Username, _options.Password);
        }

        var mqttOptions = optionsBuilder.Build();

        _logger.LogInformation("Connecting to MQTT broker at {Host}:{Port}...", _options.Host, _options.Port);
        
        await _mqttClient.ConnectAsync(mqttOptions, stoppingToken);
        
        _logger.LogInformation("Connected to MQTT broker successfully");

        // Subscribe to device topics using wildcards
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter($"{_options.TopicPrefix}/+/transition", MqttQualityOfServiceLevel.AtLeastOnce)
            .WithTopicFilter($"{_options.TopicPrefix}/+/status", MqttQualityOfServiceLevel.AtLeastOnce)
            .WithTopicFilter($"{_options.TopicPrefix}/+/distance", MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        await _mqttClient.SubscribeAsync(subscribeOptions, stoppingToken);
        
        _logger.LogInformation("Subscribed to topics: {Prefix}/+/transition, {Prefix}/+/status, {Prefix}/+/distance", 
            _options.TopicPrefix, _options.TopicPrefix, _options.TopicPrefix);
    }

    private async Task DisconnectAsync()
    {
        if (_mqttClient?.IsConnected == true)
        {
            try
            {
                await _mqttClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting MQTT client");
            }
        }

        _mqttClient?.Dispose();
        _mqttClient = null;
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("Cannot publish - MQTT client not connected");
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(message, cancellationToken);
        _logger.LogDebug("Published to {Topic}: {Payload}", topic, payload);
    }

    public async Task SendCalibrateCommand(string macAddress, CancellationToken cancellationToken = default)
    {
        var topic = $"{_options.TopicPrefix}/{macAddress}/command";
        var payload = JsonSerializer.Serialize(new { action = "calibrate" });
        
        await PublishAsync(topic, payload, cancellationToken);
        _logger.LogInformation("Sent calibrate command to device {Mac}", macAddress);
    }

    public async Task SendSettingsUpdate(string macAddress, DeviceSettings settings, CancellationToken cancellationToken = default)
    {
        var topic = $"{_options.TopicPrefix}/{macAddress}/command";
        var payload = JsonSerializer.Serialize(new
        {
            action = "updateSettings",
            payload = new
            {
                thresholdFront = settings.ThresholdFront,
                thresholdBack = settings.ThresholdBack,
                detectionTimeoutMs = settings.DetectionTimeoutMs,
                cooldownMs = settings.CooldownMs
            }
        });
        
        await PublishAsync(topic, payload, cancellationToken);
        _logger.LogInformation("Sent settings update to device {Mac}", macAddress);
    }

    public async Task SendEnableDistanceStream(string macAddress, bool enabled, CancellationToken cancellationToken = default)
    {
        var topic = $"{_options.TopicPrefix}/{macAddress}/command";
        var payload = JsonSerializer.Serialize(new { action = "enableDistance", enabled });
        
        await PublishAsync(topic, payload, cancellationToken);
        _logger.LogInformation("Sent enableDistance={Enabled} to device {Mac}", enabled, macAddress);
    }
}
