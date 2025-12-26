namespace Koornik.Contracts.Requests;

public class UpdateDeviceRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public Guid FrontZoneId { get; set; }
    public Guid BackZoneId { get; set; }
}
