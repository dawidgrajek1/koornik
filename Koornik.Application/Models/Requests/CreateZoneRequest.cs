using System.ComponentModel.DataAnnotations;

namespace Koornik.Application.Models.Requests;

public class CreateZoneRequest
{
    [Required]
    [MaxLength(32)]
    public string Name { get; set; } = string.Empty;
    [Required]
    [Range(0, 1_000_000)]
    public int Capacity { get; set; }
}