using FluentAssertions;
using Koornik.Application.Services;
using Koornik.Contracts.Requests;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Core.Specifications;
using Koornik.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Koornik.Application.Tests;

public class DeviceServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IBaseRepository<Device>> _deviceRepository;
    private readonly Mock<ILogger<DeviceService>> _logger;

    public DeviceServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _deviceRepository = new Mock<IBaseRepository<Device>>();
        _logger = new Mock<ILogger<DeviceService>>();

        _unitOfWork.Setup(u => u.Repository<Device>()).Returns(_deviceRepository.Object);
    }

    private DeviceService CreateSut() => new(_unitOfWork.Object, _logger.Object);

    #region CreateDevice Tests

    [Fact]
    public async Task CreateDevice_WithValidRequest_ReturnsDeviceResponse()
    {
        // Arrange
        var request = new CreateDeviceRequest
        {
            Name = "Test Device",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            FrontZoneId = Guid.NewGuid(),
            BackZoneId = Guid.NewGuid()
        };

        var createdDevice = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            FrontZoneId = request.FrontZoneId,
            BackZoneId = request.BackZoneId
        };

        _deviceRepository.Setup(r => r.Add(It.IsAny<Device>())).ReturnsAsync(createdDevice);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be(request.Name);
        result.Value.IpAddress.Should().Be(request.IpAddress);
        result.Value.MacAddress.Should().Be(request.MacAddress);
        result.Value.FrontZoneId.Should().Be(request.FrontZoneId);
        result.Value.BackZoneId.Should().Be(request.BackZoneId);

        _deviceRepository.Verify(r => r.Add(It.IsAny<Device>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateDevice_WithEmptyName_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateDeviceRequest
        {
            Name = "",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF"
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device name cannot be empty.");
    }

    [Fact]
    public async Task CreateDevice_WithEmptyIpAddress_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateDeviceRequest
        {
            Name = "Test Device",
            IpAddress = "",
            MacAddress = "AA:BB:CC:DD:EE:FF"
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "IP address cannot be empty.");
    }

    [Theory]
    [InlineData("192.168.1")] // Too few octets
    [InlineData("192.168.1.1.1")] // Too many octets
    [InlineData("192.168.1.256")] // Octet > 255
    [InlineData("192.168.1.-1")] // Negative octet
    [InlineData("192.168.1.abc")] // Non-numeric octet
    [InlineData("invalid")] // Completely invalid
    public async Task CreateDevice_WithInvalidIpFormat_ReturnsValidationError(string invalidIp)
    {
        // Arrange
        var request = new CreateDeviceRequest
        {
            Name = "Test Device",
            IpAddress = invalidIp,
            MacAddress = "AA:BB:CC:DD:EE:FF"
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Invalid IP address format.");
    }

    [Fact]
    public async Task CreateDevice_WithEmptyMacAddress_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateDeviceRequest
        {
            Name = "Test Device",
            IpAddress = "192.168.1.100",
            MacAddress = ""
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "MAC address cannot be empty.");
    }

    [Fact]
    public async Task CreateDevice_WithMultipleInvalidFields_ReturnsAllErrors()
    {
        // Arrange
        var request = new CreateDeviceRequest
        {
            Name = "",
            IpAddress = "",
            MacAddress = "",
            FrontZoneId = null,
            BackZoneId = null
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(4);
        result.Errors.Should().Contain(e => e.Description == "Device name cannot be empty.");
        result.Errors.Should().Contain(e => e.Description == "IP address cannot be empty.");
        result.Errors.Should().Contain(e => e.Description == "MAC address cannot be empty.");
        result.Errors.Should().Contain(e => e.Description == "At least one zone (front or back) must be specified.");
    }

    [Fact]
    public async Task CreateDevice_WithOnlyFrontZone_SucceedsAsBorderDevice()
    {
        // Arrange - Border device with only front zone (back is "Outside")
        var request = new CreateDeviceRequest
        {
            Name = "Border Device",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            FrontZoneId = Guid.NewGuid(),
            BackZoneId = null
        };

        var createdDevice = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            FrontZoneId = request.FrontZoneId,
            BackZoneId = null
        };

        _deviceRepository.Setup(r => r.Add(It.IsAny<Device>())).ReturnsAsync(createdDevice);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.FrontZoneId.Should().Be(request.FrontZoneId);
        result.Value.BackZoneId.Should().BeNull();
    }

    [Fact]
    public async Task CreateDevice_WithOnlyBackZone_SucceedsAsBorderDevice()
    {
        // Arrange - Border device with only back zone (front is "Outside")
        var request = new CreateDeviceRequest
        {
            Name = "Border Device",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            FrontZoneId = null,
            BackZoneId = Guid.NewGuid()
        };

        var createdDevice = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            FrontZoneId = null,
            BackZoneId = request.BackZoneId
        };

        _deviceRepository.Setup(r => r.Add(It.IsAny<Device>())).ReturnsAsync(createdDevice);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.FrontZoneId.Should().BeNull();
        result.Value.BackZoneId.Should().Be(request.BackZoneId);
    }

    [Fact]
    public async Task CreateDevice_WithNoZones_ReturnsValidationError()
    {
        // Arrange - At least one zone is required
        var request = new CreateDeviceRequest
        {
            Name = "Invalid Device",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            FrontZoneId = null,
            BackZoneId = null
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateDevice(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "At least one zone (front or back) must be specified.");
    }

    #endregion

    #region GetAllDevices Tests

    [Fact]
    public async Task GetAllDevices_WhenDevicesExist_ReturnsDeviceList()
    {
        // Arrange
        var devices = new List<Device>
        {
            new() { Id = Guid.NewGuid(), Name = "Device 1", IpAddress = "192.168.1.1", MacAddress = "AA:AA:AA:AA:AA:AA" },
            new() { Id = Guid.NewGuid(), Name = "Device 2", IpAddress = "192.168.1.2", MacAddress = "BB:BB:BB:BB:BB:BB" }
        };

        _deviceRepository.Setup(r => r.ListAll()).ReturnsAsync(devices);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllDevices();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Select(d => d.Name).Should().Contain(new[] { "Device 1", "Device 2" });
    }

    [Fact]
    public async Task GetAllDevices_WhenNoDevices_ReturnsEmptyList()
    {
        // Arrange
        _deviceRepository.Setup(r => r.ListAll()).ReturnsAsync(new List<Device>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllDevices();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GetDeviceById Tests

    [Fact]
    public async Task GetDeviceById_WhenDeviceExists_ReturnsDevice()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "Test Device",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF"
        };

        _deviceRepository.Setup(r => r.GetById(deviceId)).ReturnsAsync(device);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceById(deviceId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(deviceId);
        result.Value.Name.Should().Be("Test Device");
    }

    [Fact]
    public async Task GetDeviceById_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceById(Guid.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device id should not be empty.");
    }

    [Fact]
    public async Task GetDeviceById_WhenDeviceNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _deviceRepository.Setup(r => r.GetById(deviceId)).ReturnsAsync((Device?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceById(deviceId);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device not found.");
    }

    #endregion

    #region GetDeviceByMacAddress Tests

    [Fact]
    public async Task GetDeviceByMacAddress_WhenDeviceExists_ReturnsDevice()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Test Device",
            IpAddress = "192.168.1.100",
            MacAddress = macAddress
        };

        _deviceRepository
            .Setup(r => r.FirstOrDefault(It.IsAny<ISpecification<Device>>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceByMacAddress(macAddress);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.MacAddress.Should().Be(macAddress);
    }

    [Fact]
    public async Task GetDeviceByMacAddress_WithEmptyMacAddress_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceByMacAddress("");

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "MAC address cannot be empty.");
    }

    [Fact]
    public async Task GetDeviceByMacAddress_WhenDeviceNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _deviceRepository
            .Setup(r => r.FirstOrDefault(It.IsAny<ISpecification<Device>>()))
            .ReturnsAsync((Device?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceByMacAddress("AA:BB:CC:DD:EE:FF");

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device not found.");
    }

    #endregion

    #region GetDeviceByZoneId Tests

    [Fact]
    public async Task GetDeviceByZoneId_WhenDeviceExists_ReturnsDevice()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Test Device",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            FrontZoneId = zoneId
        };

        _deviceRepository
            .Setup(r => r.FirstOrDefault(It.IsAny<ISpecification<Device>>()))
            .ReturnsAsync(device);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceByZoneId(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.FrontZoneId.Should().Be(zoneId);
    }

    [Fact]
    public async Task GetDeviceByZoneId_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceByZoneId(Guid.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone id cannot be empty.");
    }

    [Fact]
    public async Task GetDeviceByZoneId_WhenDeviceNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _deviceRepository
            .Setup(r => r.FirstOrDefault(It.IsAny<ISpecification<Device>>()))
            .ReturnsAsync((Device?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDeviceByZoneId(Guid.NewGuid());

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device not found.");
    }

    #endregion

    #region UpdateDevice Tests

    [Fact]
    public async Task UpdateDevice_WithValidRequest_ReturnsUpdatedDevice()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var existingDevice = new Device
        {
            Id = deviceId,
            Name = "Old Name",
            IpAddress = "192.168.1.1",
            MacAddress = "AA:AA:AA:AA:AA:AA",
            FrontZoneId = Guid.NewGuid(),
            BackZoneId = Guid.NewGuid()
        };

        var updateRequest = new UpdateDeviceRequest
        {
            Id = deviceId,
            Name = "New Name",
            IpAddress = "192.168.1.200",
            MacAddress = "BB:BB:BB:BB:BB:BB",
            FrontZoneId = Guid.NewGuid(),
            BackZoneId = Guid.NewGuid()
        };

        _deviceRepository.Setup(r => r.GetById(deviceId)).ReturnsAsync(existingDevice);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateDevice(deviceId, updateRequest);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("New Name");
        result.Value.IpAddress.Should().Be("192.168.1.200");
        result.Value.MacAddress.Should().Be("BB:BB:BB:BB:BB:BB");

        _deviceRepository.Verify(r => r.Update(It.IsAny<Device>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateDevice_WithEmptyId_ReturnsValidationError()
    {
        // Arrange
        var updateRequest = new UpdateDeviceRequest
        {
            Name = "New Name",
            IpAddress = "192.168.1.200",
            MacAddress = "BB:BB:BB:BB:BB:BB"
        };

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateDevice(Guid.Empty, updateRequest);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device id should not be empty.");
    }

    [Fact]
    public async Task UpdateDevice_WhenDeviceNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var updateRequest = new UpdateDeviceRequest
        {
            Name = "New Name",
            IpAddress = "192.168.1.200",
            MacAddress = "BB:BB:BB:BB:BB:BB",
            FrontZoneId = Guid.NewGuid() // At least one zone required
        };

        _deviceRepository.Setup(r => r.GetById(deviceId)).ReturnsAsync((Device?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateDevice(deviceId, updateRequest);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device not found.");
    }

    #endregion

    #region DeleteDevice Tests

    [Fact]
    public async Task DeleteDevice_WhenDeviceExists_ReturnsDeleted()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "Test Device",
            IpAddress = "192.168.1.100",
            MacAddress = "AA:BB:CC:DD:EE:FF"
        };

        _deviceRepository.Setup(r => r.GetById(deviceId)).ReturnsAsync(device);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.DeleteDevice(deviceId);

        // Assert
        result.IsError.Should().BeFalse();
        _deviceRepository.Verify(r => r.Delete(device), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteDevice_WhenDeviceNotFound_ReturnsDeleted()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _deviceRepository.Setup(r => r.GetById(deviceId)).ReturnsAsync((Device?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.DeleteDevice(deviceId);

        // Assert
        result.IsError.Should().BeFalse();
        _deviceRepository.Verify(r => r.Delete(It.IsAny<Device>()), Times.Never);
    }

    #endregion
}
