using System.ComponentModel.DataAnnotations;
using Koornik.Domain.Core.Models;

namespace Koornik.Domain.Entities;

public class Zone : BaseEntity
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; init; } = string.Empty;
    public int Capacity { get; init; }
}