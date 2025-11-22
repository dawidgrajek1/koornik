using System.ComponentModel.DataAnnotations;
using Koornik.Domain.Core.Models;

namespace Koornik.Domain.Entities;

public class ZoneTransition : BaseEntity
{
    [Key]
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public Guid FromZone { get; set; }
    public Guid ToZone { get; set; }
}