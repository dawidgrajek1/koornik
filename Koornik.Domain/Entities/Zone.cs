using System.ComponentModel.DataAnnotations;
using Koornik.Domain.Core.Models;

namespace Koornik.Domain.Entities;

public class Zone : BaseEntity
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
}