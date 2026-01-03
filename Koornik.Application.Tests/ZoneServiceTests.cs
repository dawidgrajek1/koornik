using FluentAssertions;
using Koornik.Application.Services;
using Koornik.Contracts.Requests;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Core.Specifications;
using Koornik.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Koornik.Application.Tests;

public class ZoneServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IBaseRepository<Zone>> _zoneRepository;
    private readonly Mock<IBaseRepository<Transition>> _transitionRepository;
    private readonly Mock<IBaseRepository<ZoneStateSnapshot>> _snapshotRepository;
    private readonly Mock<ILogger<ZoneService>> _logger;

    public ZoneServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _zoneRepository = new Mock<IBaseRepository<Zone>>();
        _transitionRepository = new Mock<IBaseRepository<Transition>>();
        _snapshotRepository = new Mock<IBaseRepository<ZoneStateSnapshot>>();
        _logger = new Mock<ILogger<ZoneService>>();

        _unitOfWork.Setup(u => u.Repository<Zone>()).Returns(_zoneRepository.Object);
        _unitOfWork.Setup(u => u.Repository<Transition>()).Returns(_transitionRepository.Object);
        _unitOfWork.Setup(u => u.Repository<ZoneStateSnapshot>()).Returns(_snapshotRepository.Object);
    }

    private ZoneService CreateSut() => new(_unitOfWork.Object, _logger.Object);

    #region CreateZone Tests

    [Fact]
    public async Task CreateZone_WithValidRequest_ReturnsZoneResponse()
    {
        // Arrange
        var request = new CreateZoneRequest
        {
            Name = "Test Zone",
            Capacity = 100,
            GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[]]}"
        };

        var createdZone = new Zone
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Capacity = request.Capacity,
            CurrentOccupancy = 0,
            GeoJson = request.GeoJson
        };

        _zoneRepository.Setup(r => r.Add(It.IsAny<Zone>())).ReturnsAsync(createdZone);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateZone(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Capacity.Should().Be(request.Capacity);
        result.Value.CurrentOccupancy.Should().Be(0);
        result.Value.GeoJson.Should().Be(request.GeoJson);

        _zoneRepository.Verify(r => r.Add(It.IsAny<Zone>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateZone_WithoutGeoJson_ReturnsZoneResponseWithNullGeoJson()
    {
        // Arrange
        var request = new CreateZoneRequest
        {
            Name = "Test Zone",
            Capacity = 100,
            GeoJson = null
        };

        var createdZone = new Zone
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Capacity = request.Capacity,
            CurrentOccupancy = 0,
            GeoJson = null
        };

        _zoneRepository.Setup(r => r.Add(It.IsAny<Zone>())).ReturnsAsync(createdZone);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateZone(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.GeoJson.Should().BeNull();
    }

    [Fact]
    public async Task CreateZone_WithEmptyName_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateZoneRequest
        {
            Name = "",
            Capacity = 100
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateZone(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone name should not be empty.");
    }

    [Fact]
    public async Task CreateZone_WithZeroCapacity_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateZoneRequest
        {
            Name = "Test Zone",
            Capacity = 0
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateZone(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone capacity should not be less than 1.");
    }

    [Fact]
    public async Task CreateZone_WithNegativeCapacity_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateZoneRequest
        {
            Name = "Test Zone",
            Capacity = -5
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateZone(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone capacity should not be less than 1.");
    }

    [Fact]
    public async Task CreateZone_WithMultipleInvalidFields_ReturnsAllErrors()
    {
        // Arrange
        var request = new CreateZoneRequest
        {
            Name = "",
            Capacity = 0
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateZone(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Description == "Zone name should not be empty.");
        result.Errors.Should().Contain(e => e.Description == "Zone capacity should not be less than 1.");
    }

    #endregion

    #region GetZoneById Tests

    [Fact]
    public async Task GetZoneById_WhenZoneExists_ReturnsZone()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var zone = new Zone
        {
            Id = zoneId,
            Name = "Test Zone",
            Capacity = 100,
            CurrentOccupancy = 25,
            GeoJson = "{\"type\":\"Polygon\"}"
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(zone);

        var sut = CreateSut();

        // Act
        var result = await sut.GetZoneById(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(zoneId);
        result.Value.Name.Should().Be("Test Zone");
        result.Value.Capacity.Should().Be(100);
        result.Value.CurrentOccupancy.Should().Be(25);
        result.Value.GeoJson.Should().Be("{\"type\":\"Polygon\"}");
    }

    [Fact]
    public async Task GetZoneById_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetZoneById(Guid.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone id should not be empty.");
    }

    [Fact]
    public async Task GetZoneById_WhenZoneNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync((Zone?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetZoneById(zoneId);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone not found.");
    }

    #endregion

    #region GetAllZones Tests

    [Fact]
    public async Task GetAllZones_WhenZonesExist_ReturnsZoneList()
    {
        // Arrange
        var zones = new List<Zone>
        {
            new() { Id = Guid.NewGuid(), Name = "Zone 1", Capacity = 50 },
            new() { Id = Guid.NewGuid(), Name = "Zone 2", Capacity = 100 }
        };

        _zoneRepository.Setup(r => r.ListAll()).ReturnsAsync(zones);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllZones();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Select(z => z.Name).Should().Contain(new[] { "Zone 1", "Zone 2" });
    }

    [Fact]
    public async Task GetAllZones_WhenNoZones_ReturnsEmptyList()
    {
        // Arrange
        _zoneRepository.Setup(r => r.ListAll()).ReturnsAsync(new List<Zone>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllZones();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region UpdateZone Tests

    [Fact]
    public async Task UpdateZone_WithValidRequest_ReturnsUpdatedZone()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var existingZone = new Zone
        {
            Id = zoneId,
            Name = "Old Name",
            Capacity = 50,
            CurrentOccupancy = 10,
            GeoJson = null
        };

        var updateRequest = new UpdateZoneRequest
        {
            Id = zoneId,
            Name = "New Name",
            Capacity = 100,
            GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[]]}"
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(existingZone);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateZone(zoneId, updateRequest);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("New Name");
        result.Value.Capacity.Should().Be(100);
        result.Value.CurrentOccupancy.Should().Be(10); // Should remain unchanged
        result.Value.GeoJson.Should().Be("{\"type\":\"Polygon\",\"coordinates\":[[]]}");

        _zoneRepository.Verify(r => r.Update(It.IsAny<Zone>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateZone_WithEmptyId_ReturnsValidationError()
    {
        // Arrange
        var updateRequest = new UpdateZoneRequest
        {
            Name = "New Name",
            Capacity = 100
        };

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateZone(Guid.Empty, updateRequest);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone id should not be empty.");
    }

    [Fact]
    public async Task UpdateZone_WhenZoneNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var updateRequest = new UpdateZoneRequest
        {
            Name = "New Name",
            Capacity = 100
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync((Zone?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.UpdateZone(zoneId, updateRequest);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone not found.");
    }

    #endregion

    #region DeleteZone Tests

    [Fact]
    public async Task DeleteZone_WhenZoneExists_ReturnsDeleted()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var zone = new Zone
        {
            Id = zoneId,
            Name = "Test Zone",
            Capacity = 100
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(zone);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.DeleteZone(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        _zoneRepository.Verify(r => r.Delete(zone), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteZone_WhenZoneNotFound_ReturnsDeleted()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync((Zone?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.DeleteZone(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        _zoneRepository.Verify(r => r.Delete(It.IsAny<Zone>()), Times.Never);
    }

    #endregion

    #region GetCurrentState Tests

    [Fact]
    public async Task GetCurrentState_WithValidZoneId_ReturnsZoneState()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var zone = new Zone
        {
            Id = zoneId,
            Name = "Test Zone",
            Capacity = 100
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(zone);
        _snapshotRepository.Setup(r => r.List(It.IsAny<ISpecification<ZoneStateSnapshot>>()))
            .ReturnsAsync(new List<ZoneStateSnapshot>());
        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(new List<Transition>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentState(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.ZoneId.Should().Be(zoneId);
        result.Value.ZoneName.Should().Be("Test Zone");
        result.Value.Capacity.Should().Be(100);
        result.Value.OccupancyCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentState_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentState(Guid.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone id should not be empty.");
    }

    [Fact]
    public async Task GetCurrentState_WhenZoneNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync((Zone?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentState(zoneId);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone not found.");
    }

    [Fact]
    public async Task GetCurrentState_ReturnsStoredOccupancy()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var zone = new Zone
        {
            Id = zoneId,
            Name = "Test Zone",
            Capacity = 100,
            CurrentOccupancy = 25  // Stored occupancy
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(zone);

        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentState(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.OccupancyCount.Should().Be(25); // Should return stored value directly
        result.Value.Capacity.Should().Be(100);
        result.Value.ZoneName.Should().Be("Test Zone");
    }

    [Fact]
    public async Task GetCurrentState_WithZeroOccupancy_ReturnsZero()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var zone = new Zone
        {
            Id = zoneId,
            Name = "Empty Zone",
            Capacity = 50,
            CurrentOccupancy = 0
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(zone);

        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentState(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.OccupancyCount.Should().Be(0);
    }

    #endregion

    #region GetStateAt Tests

    [Fact]
    public async Task GetStateAt_WithValidParameters_ReturnsZoneState()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddHours(-2);
        var zone = new Zone
        {
            Id = zoneId,
            Name = "Test Zone",
            Capacity = 100
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(zone);
        _snapshotRepository.Setup(r => r.List(It.IsAny<ISpecification<ZoneStateSnapshot>>()))
            .ReturnsAsync(new List<ZoneStateSnapshot>());
        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(new List<Transition>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetStateAt(zoneId, timestamp);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.ZoneId.Should().Be(zoneId);
        result.Value.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public async Task GetStateAt_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetStateAt(Guid.Empty, DateTime.UtcNow);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone id should not be empty.");
    }

    [Fact]
    public async Task GetStateAt_WhenZoneNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync((Zone?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetStateAt(zoneId, DateTime.UtcNow);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone not found.");
    }

    #endregion

    #region GetStateHistory Tests

    [Fact]
    public async Task GetStateHistory_WithValidParameters_ReturnsHistory()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;
        var zone = new Zone
        {
            Id = zoneId,
            Name = "Test Zone",
            Capacity = 100
        };

        var snapshots = new List<ZoneStateSnapshot>
        {
            new() { Id = Guid.NewGuid(), ZoneId = zoneId, OccupancyCount = 10, Capacity = 100, CreatedAt = startDate.AddHours(1) },
            new() { Id = Guid.NewGuid(), ZoneId = zoneId, OccupancyCount = 15, Capacity = 100, CreatedAt = startDate.AddHours(2) }
        };

        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync(zone);
        _snapshotRepository.Setup(r => r.List(It.IsAny<ISpecification<ZoneStateSnapshot>>()))
            .ReturnsAsync(snapshots);

        var sut = CreateSut();

        // Act
        var result = await sut.GetStateHistory(zoneId, startDate, endDate);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.ZoneId.Should().Be(zoneId);
        result.Value.ZoneName.Should().Be("Test Zone");
        result.Value.StartDate.Should().Be(startDate);
        result.Value.EndDate.Should().Be(endDate);
        result.Value.States.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStateHistory_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetStateHistory(Guid.Empty, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone id should not be empty.");
    }

    [Fact]
    public async Task GetStateHistory_WithStartDateAfterEndDate_ReturnsValidationError()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var sut = CreateSut();

        // Act
        var result = await sut.GetStateHistory(zoneId, DateTime.UtcNow, DateTime.UtcNow.AddDays(-1));

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Start date must be before end date.");
    }

    [Fact]
    public async Task GetStateHistory_WhenZoneNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        _zoneRepository.Setup(r => r.GetById(zoneId)).ReturnsAsync((Zone?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetStateHistory(zoneId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone not found.");
    }

    #endregion
}
