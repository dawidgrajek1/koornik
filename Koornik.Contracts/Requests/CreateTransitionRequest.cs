namespace Koornik.Contracts.Requests;

public class CreateTransitionRequest
{
    public Guid DeviceId { get; set; }
    public Guid? FromZoneId { get; set; }
    public Guid? ToZoneId { get; set; }
    public DateTime? CreatedAt { get; set; }
}
