using System.Text;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Server;

namespace Koornik.Infrastructure.Services;

public class MqttService : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<MqttService> _logger;
    private MqttServer? _mqttServer;

    // MQTT Topics
    private static class Topics
    {
        public const string Direction = "tof/direction";   // Publish: "IN" or "OUT" when passage detected
        public const string Calibrate = "tof/calibrate";   // Subscribe: Send any message to trigger calibration
        public const string Status = "tof/status";         // Publish: Status messages (online, calibrating, calibrated:...)
    }

    public event EventHandler<DirectionEventArgs>? DirectionDetected;
    public event EventHandler? CalibrationRequested;
    public event EventHandler<StatusEventArgs>? StatusReceived;

    public MqttService(ILogger<MqttService> logger)
    {
        _logger = logger;
    }

    public async Task Run()
    {
        var mqttFactory = new MqttServerFactory();
        var mqttServerOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .Build();

        _mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

        // Handle incoming messages
        _mqttServer.InterceptingPublishAsync += HandleIncomingMessage;

        // Handle client connections
        _mqttServer.ClientConnectedAsync += args =>
        {
            _logger.LogInformation("Client connected: {ClientId}", args.ClientId);
            return Task.CompletedTask;
        };

        _mqttServer.ClientDisconnectedAsync += args =>
        {
            _logger.LogInformation("Client disconnected: {ClientId}", args.ClientId);
            return Task.CompletedTask;
        };

        await _mqttServer.StartAsync();
        _logger.LogInformation("MQTT Server started");

        while (!_cts.Token.IsCancellationRequested)
        {
            await Task.Delay(1000, _cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await _mqttServer.StopAsync();
        _logger.LogInformation("MQTT Server stopped");
    }

    private Task HandleIncomingMessage(InterceptingPublishEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.Payload is { Length: > 0 } 
            ? Encoding.UTF8.GetString(args.ApplicationMessage.Payload) 
            : string.Empty;

        _logger.LogDebug("Received message on topic '{Topic}': {Payload}", topic, payload);

        switch (topic)
        {
            case Topics.Direction:
                HandleDirectionMessage(payload);
                break;
            case Topics.Calibrate:
                HandleCalibrateMessage();
                break;
            case Topics.Status:
                HandleStatusMessage(payload);
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleDirectionMessage(string payload)
    {
        var direction = payload.Trim().ToUpperInvariant();
        if (direction is "IN" or "OUT")
        {
            _logger.LogInformation("Direction detected: {Direction}", direction);
            DirectionDetected?.Invoke(this, new DirectionEventArgs(direction));
        }
        else
        {
            _logger.LogWarning("Invalid direction payload: {Payload}", payload);
        }
    }

    private void HandleCalibrateMessage()
    {
        _logger.LogInformation("Calibration requested");
        CalibrationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HandleStatusMessage(string payload)
    {
        _logger.LogInformation("Status received: {Status}", payload);
        StatusReceived?.Invoke(this, new StatusEventArgs(payload));
    }

    /// <summary>
    /// Publishes a direction message ("IN" or "OUT") to the tof/direction topic
    /// </summary>
    public async Task PublishDirectionAsync(string direction)
    {
        if (_mqttServer is null)
        {
            _logger.LogWarning("Cannot publish direction: MQTT server not running");
            return;
        }

        var message = new InjectedMqttApplicationMessage(
            new MqttApplicationMessageBuilder()
                .WithTopic(Topics.Direction)
                .WithPayload(direction)
                .Build());

        await _mqttServer.InjectApplicationMessage(message);
        _logger.LogDebug("Published direction: {Direction}", direction);
    }

    /// <summary>
    /// Publishes a status message to the tof/status topic
    /// </summary>
    public async Task PublishStatusAsync(string status)
    {
        if (_mqttServer is null)
        {
            _logger.LogWarning("Cannot publish status: MQTT server not running");
            return;
        }

        var message = new InjectedMqttApplicationMessage(
            new MqttApplicationMessageBuilder()
                .WithTopic(Topics.Status)
                .WithPayload(status)
                .Build());

        await _mqttServer.InjectApplicationMessage(message);
        _logger.LogDebug("Published status: {Status}", status);
    }

    /// <summary>
    /// Sends a calibration trigger message to the tof/calibrate topic
    /// </summary>
    public async Task TriggerCalibrationAsync()
    {
        if (_mqttServer is null)
        {
            _logger.LogWarning("Cannot trigger calibration: MQTT server not running");
            return;
        }

        var message = new InjectedMqttApplicationMessage(
            new MqttApplicationMessageBuilder()
                .WithTopic(Topics.Calibrate)
                .WithPayload("calibrate")
                .Build());

        await _mqttServer.InjectApplicationMessage(message);
        _logger.LogDebug("Calibration triggered");
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        _cts.Dispose();
        _mqttServer?.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class DirectionEventArgs : EventArgs
{
    public string Direction { get; }

    public DirectionEventArgs(string direction)
    {
        Direction = direction;
    }
}

public class StatusEventArgs : EventArgs
{
    public string Status { get; }

    public StatusEventArgs(string status)
    {
        Status = status;
    }
}

