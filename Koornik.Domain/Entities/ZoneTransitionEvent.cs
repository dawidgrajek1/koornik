using System.ComponentModel.DataAnnotations;
using Koornik.Domain.Core.Models;

namespace Koornik.Domain.Entities;

public class ZoneTransitionEvent : BaseEntity, IBaseEvent
{
    [Key]
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid FromZone { get; set; }
    public Guid ToZone { get; set; }
}