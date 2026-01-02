namespace Koornik.Infrastructure.Options;

public class MqttOptions
{
    public const string SectionName = "Mqtt";
    
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "Koornik.API";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public string TopicPrefix { get; set; } = "koornik";
}
