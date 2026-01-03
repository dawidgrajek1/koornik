namespace Koornik.Contracts.Requests;

public class UpdateZoneRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? GeoJson { get; set; }
}
