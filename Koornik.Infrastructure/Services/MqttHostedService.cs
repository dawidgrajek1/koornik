using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Koornik.Infrastructure.Services;

public class MqttHostedService : IHostedService
{
    private readonly MqttService _mqttService;
    private readonly ILogger<MqttHostedService> _logger;
    private Task? _runTask;

    public MqttHostedService(MqttService mqttService, ILogger<MqttHostedService> logger)
    {
        _mqttService = mqttService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MQTT broker...");
        _runTask = Task.Run(() => _mqttService.Run(), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MQTT broker...");
        _mqttService.Stop();
        return _runTask ?? Task.CompletedTask;
    }
}

