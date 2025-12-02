namespace Koornik.Contracts.Responses;

public class TransitionResponse
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid FromZoneId { get; set; }
    public Guid ToZoneId { get; set; }
}
