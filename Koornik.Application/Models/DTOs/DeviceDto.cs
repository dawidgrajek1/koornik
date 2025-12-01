using Koornik.Domain.Entities;

namespace Koornik.Application.Models.DTOs;

public class DeviceDto(Device device)
{
    public Guid Id { get; set; } = device.Id;
    public string Name { get; set; } = device.Name;
    public string IpAddress { get; set; } = device.IpAddress;
    public Guid FrontZoneId { get; set; } = device.FrontZoneId;
    public Guid BackZoneId { get; set; } = device.BackZoneId;
}