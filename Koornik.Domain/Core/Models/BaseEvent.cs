namespace Koornik.Domain.Core.Models;

public interface IBaseEvent
{
    public DateTime CreatedAt { get; set; }
}