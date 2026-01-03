namespace Koornik.Contracts.Responses;

public class DeviceResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public Guid? FrontZoneId { get; init; }
    public Guid? BackZoneId { get; init; }
}
