namespace Koornik.Contracts.Requests;

public class CreateZoneRequest
{
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
}
