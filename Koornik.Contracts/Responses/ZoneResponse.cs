namespace Koornik.Contracts.Responses;

public class ZoneResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int CurrentOccupancy { get; set; }
    public string? GeoJson { get; set; }
}
