using FluentAssertions;
using Koornik.Application.Services;
using Koornik.Contracts.Requests;
using Koornik.Domain.Core.Repositories;
using Koornik.Domain.Core.Specifications;
using Koornik.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Koornik.Application.Tests;

public class TransitionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IBaseRepository<Transition>> _transitionRepository;
    private readonly Mock<IBaseRepository<Zone>> _zoneRepository;
    private readonly Mock<ILogger<TransitionService>> _logger;

    public TransitionServiceTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _transitionRepository = new Mock<IBaseRepository<Transition>>();
        _zoneRepository = new Mock<IBaseRepository<Zone>>();
        _logger = new Mock<ILogger<TransitionService>>();

        _unitOfWork.Setup(u => u.Repository<Transition>()).Returns(_transitionRepository.Object);
        _unitOfWork.Setup(u => u.Repository<Zone>()).Returns(_zoneRepository.Object);
    }

    private TransitionService CreateSut() => new(_unitOfWork.Object, _logger.Object);

    #region CreateTransition Tests

    [Fact]
    public async Task CreateTransition_WithValidRequest_ReturnsTransitionResponse()
    {
        // Arrange
        var fromZoneId = Guid.NewGuid();
        var toZoneId = Guid.NewGuid();
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = fromZoneId,
            ToZoneId = toZoneId,
            CreatedAt = DateTime.UtcNow
        };

        var fromZone = new Zone { Id = fromZoneId, Name = "From Zone", Capacity = 100, CurrentOccupancy = 5 };
        var toZone = new Zone { Id = toZoneId, Name = "To Zone", Capacity = 100, CurrentOccupancy = 10 };

        var createdTransition = new Transition
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            FromZone = request.FromZoneId,
            ToZone = request.ToZoneId,
            CreatedAt = request.CreatedAt.Value
        };

        _zoneRepository.Setup(r => r.GetById(fromZoneId)).ReturnsAsync(fromZone);
        _zoneRepository.Setup(r => r.GetById(toZoneId)).ReturnsAsync(toZone);
        _transitionRepository.Setup(r => r.Add(It.IsAny<Transition>())).ReturnsAsync(createdTransition);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.DeviceId.Should().Be(request.DeviceId);
        result.Value.FromZoneId.Should().Be(request.FromZoneId);
        result.Value.ToZoneId.Should().Be(request.ToZoneId);

        // Verify occupancy was updated
        fromZone.CurrentOccupancy.Should().Be(4); // Decremented from 5 to 4
        toZone.CurrentOccupancy.Should().Be(11);  // Incremented from 10 to 11

        _zoneRepository.Verify(r => r.Update(fromZone), Times.Once);
        _zoneRepository.Verify(r => r.Update(toZone), Times.Once);
        _transitionRepository.Verify(r => r.Add(It.IsAny<Transition>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTransition_WithValidRequest_DoesNotDecrementBelowZero()
    {
        // Arrange
        var fromZoneId = Guid.NewGuid();
        var toZoneId = Guid.NewGuid();
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = fromZoneId,
            ToZoneId = toZoneId,
            CreatedAt = DateTime.UtcNow
        };

        var fromZone = new Zone { Id = fromZoneId, Name = "From Zone", Capacity = 100, CurrentOccupancy = 0 };
        var toZone = new Zone { Id = toZoneId, Name = "To Zone", Capacity = 100, CurrentOccupancy = 10 };

        var createdTransition = new Transition
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            FromZone = request.FromZoneId,
            ToZone = request.ToZoneId,
            CreatedAt = request.CreatedAt.Value
        };

        _zoneRepository.Setup(r => r.GetById(fromZoneId)).ReturnsAsync(fromZone);
        _zoneRepository.Setup(r => r.GetById(toZoneId)).ReturnsAsync(toZone);
        _transitionRepository.Setup(r => r.Add(It.IsAny<Transition>())).ReturnsAsync(createdTransition);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeFalse();
        fromZone.CurrentOccupancy.Should().Be(0); // Should stay at 0, not go negative
        toZone.CurrentOccupancy.Should().Be(11);
    }

    [Fact]
    public async Task CreateTransition_WhenFromZoneNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var fromZoneId = Guid.NewGuid();
        var toZoneId = Guid.NewGuid();
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = fromZoneId,
            ToZoneId = toZoneId,
            CreatedAt = DateTime.UtcNow
        };

        _zoneRepository.Setup(r => r.GetById(fromZoneId)).ReturnsAsync((Zone?)null);
        _zoneRepository.Setup(r => r.GetById(toZoneId)).ReturnsAsync(new Zone { Id = toZoneId, Name = "To Zone", Capacity = 100 });

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Source zone not found.");
    }

    [Fact]
    public async Task CreateTransition_WhenToZoneNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var fromZoneId = Guid.NewGuid();
        var toZoneId = Guid.NewGuid();
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = fromZoneId,
            ToZoneId = toZoneId,
            CreatedAt = DateTime.UtcNow
        };

        _zoneRepository.Setup(r => r.GetById(fromZoneId)).ReturnsAsync(new Zone { Id = fromZoneId, Name = "From Zone", Capacity = 100 });
        _zoneRepository.Setup(r => r.GetById(toZoneId)).ReturnsAsync((Zone?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Destination zone not found.");
    }

    [Fact]
    public async Task CreateTransition_WithoutCreatedAt_UsesCurrentTime()
    {
        // Arrange
        var fromZoneId = Guid.NewGuid();
        var toZoneId = Guid.NewGuid();
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = fromZoneId,
            ToZoneId = toZoneId,
            CreatedAt = null
        };

        var fromZone = new Zone { Id = fromZoneId, Name = "From Zone", Capacity = 100, CurrentOccupancy = 5 };
        var toZone = new Zone { Id = toZoneId, Name = "To Zone", Capacity = 100, CurrentOccupancy = 10 };

        var createdTransition = new Transition
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            FromZone = request.FromZoneId,
            ToZone = request.ToZoneId,
            CreatedAt = DateTime.UtcNow
        };

        _zoneRepository.Setup(r => r.GetById(fromZoneId)).ReturnsAsync(fromZone);
        _zoneRepository.Setup(r => r.GetById(toZoneId)).ReturnsAsync(toZone);
        _transitionRepository.Setup(r => r.Add(It.IsAny<Transition>())).ReturnsAsync(createdTransition);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeFalse();
        _transitionRepository.Verify(r => r.Add(It.Is<Transition>(t => t.CreatedAt != default)), Times.Once);
    }

    [Fact]
    public async Task CreateTransition_WithEmptyDeviceId_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.Empty,
            FromZoneId = Guid.NewGuid(),
            ToZoneId = Guid.NewGuid()
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device id should not be empty.");
    }

    [Fact]
    public async Task CreateTransition_WithNullFromZoneId_SucceedsAsBorderTransition()
    {
        // Arrange - Border device: someone entering from "Outside" to a tracked zone
        var toZoneId = Guid.NewGuid();
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = null,
            ToZoneId = toZoneId
        };

        var toZone = new Zone { Id = toZoneId, Name = "To Zone", Capacity = 100, CurrentOccupancy = 10 };

        var createdTransition = new Transition
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            FromZone = null,
            ToZone = request.ToZoneId,
            CreatedAt = DateTime.UtcNow
        };

        _zoneRepository.Setup(r => r.GetById(toZoneId)).ReturnsAsync(toZone);
        _transitionRepository.Setup(r => r.Add(It.IsAny<Transition>())).ReturnsAsync(createdTransition);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.FromZoneId.Should().BeNull();
        result.Value.ToZoneId.Should().Be(toZoneId);
        toZone.CurrentOccupancy.Should().Be(11); // Incremented from 10 to 11
    }

    [Fact]
    public async Task CreateTransition_WithNullToZoneId_SucceedsAsBorderTransition()
    {
        // Arrange - Border device: someone exiting to "Outside" from a tracked zone
        var fromZoneId = Guid.NewGuid();
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = fromZoneId,
            ToZoneId = null
        };

        var fromZone = new Zone { Id = fromZoneId, Name = "From Zone", Capacity = 100, CurrentOccupancy = 5 };

        var createdTransition = new Transition
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            FromZone = request.FromZoneId,
            ToZone = null,
            CreatedAt = DateTime.UtcNow
        };

        _zoneRepository.Setup(r => r.GetById(fromZoneId)).ReturnsAsync(fromZone);
        _transitionRepository.Setup(r => r.Add(It.IsAny<Transition>())).ReturnsAsync(createdTransition);
        _unitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.FromZoneId.Should().Be(fromZoneId);
        result.Value.ToZoneId.Should().BeNull();
        fromZone.CurrentOccupancy.Should().Be(4); // Decremented from 5 to 4
    }

    [Fact]
    public async Task CreateTransition_WithBothZonesNull_ReturnsValidationError()
    {
        // Arrange - At least one zone must be specified
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.NewGuid(),
            FromZoneId = null,
            ToZoneId = null
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "At least one zone (source or destination) must be specified.");
    }

    [Fact]
    public async Task CreateTransition_WithMultipleInvalidFields_ReturnsAllErrors()
    {
        // Arrange - Both zones null is invalid (at least one required)
        var request = new CreateTransitionRequest
        {
            DeviceId = Guid.Empty,
            FromZoneId = null,
            ToZoneId = null
        };

        var sut = CreateSut();

        // Act
        var result = await sut.CreateTransition(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Description == "Device id should not be empty.");
        result.Errors.Should().Contain(e => e.Description == "At least one zone (source or destination) must be specified.");
    }

    #endregion

    #region GetAllTransitions Tests

    [Fact]
    public async Task GetAllTransitions_WhenTransitionsExist_ReturnsTransitionList()
    {
        // Arrange
        var transitions = new List<Transition>
        {
            new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FromZone = Guid.NewGuid(), ToZone = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FromZone = Guid.NewGuid(), ToZone = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
        };

        _transitionRepository.Setup(r => r.ListAll()).ReturnsAsync(transitions);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllTransitions();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllTransitions_WhenNoTransitions_ReturnsEmptyList()
    {
        // Arrange
        _transitionRepository.Setup(r => r.ListAll()).ReturnsAsync(new List<Transition>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllTransitions();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GetTransitionById Tests

    [Fact]
    public async Task GetTransitionById_WhenTransitionExists_ReturnsTransition()
    {
        // Arrange
        var transitionId = Guid.NewGuid();
        var transition = new Transition
        {
            Id = transitionId,
            DeviceId = Guid.NewGuid(),
            FromZone = Guid.NewGuid(),
            ToZone = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        _transitionRepository.Setup(r => r.GetById(transitionId)).ReturnsAsync(transition);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionById(transitionId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(transitionId);
    }

    [Fact]
    public async Task GetTransitionById_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionById(Guid.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Transition id should not be empty.");
    }

    [Fact]
    public async Task GetTransitionById_WhenTransitionNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var transitionId = Guid.NewGuid();
        _transitionRepository.Setup(r => r.GetById(transitionId)).ReturnsAsync((Transition?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionById(transitionId);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Transition not found.");
    }

    #endregion

    #region GetTransitionsForZone Tests

    [Fact]
    public async Task GetTransitionsForZone_WhenTransitionsExist_ReturnsTransitions()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var transitions = new List<Transition>
        {
            new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FromZone = zoneId, ToZone = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FromZone = Guid.NewGuid(), ToZone = zoneId, CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
        };

        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(transitions);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForZone(zoneId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTransitionsForZone_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForZone(Guid.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Zone id should not be empty.");
    }

    [Fact]
    public async Task GetTransitionsForZone_WithDateRange_ReturnsFilteredTransitions()
    {
        // Arrange
        var zoneId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;
        var transitions = new List<Transition>
        {
            new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FromZone = zoneId, ToZone = Guid.NewGuid(), CreatedAt = startDate.AddHours(1) }
        };

        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(transitions);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForZone(zoneId, startDate, endDate);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
    }

    #endregion

    #region GetTransitionsForDevice Tests

    [Fact]
    public async Task GetTransitionsForDevice_WhenTransitionsExist_ReturnsTransitions()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var transitions = new List<Transition>
        {
            new() { Id = Guid.NewGuid(), DeviceId = deviceId, FromZone = Guid.NewGuid(), ToZone = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = Guid.NewGuid(), DeviceId = deviceId, FromZone = Guid.NewGuid(), ToZone = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
        };

        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(transitions);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForDevice(deviceId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTransitionsForDevice_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForDevice(Guid.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Description == "Device id should not be empty.");
    }

    [Fact]
    public async Task GetTransitionsForDevice_WithDateRange_ReturnsFilteredTransitions()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;
        var transitions = new List<Transition>
        {
            new() { Id = Guid.NewGuid(), DeviceId = deviceId, FromZone = Guid.NewGuid(), ToZone = Guid.NewGuid(), CreatedAt = startDate.AddHours(1) }
        };

        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(transitions);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForDevice(deviceId, startDate, endDate);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
    }

    #endregion

    #region GetTransitionsForTimeRange Tests

    [Fact]
    public async Task GetTransitionsForTimeRange_WhenTransitionsExist_ReturnsTransitions()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;
        var transitions = new List<Transition>
        {
            new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FromZone = Guid.NewGuid(), ToZone = Guid.NewGuid(), CreatedAt = startDate.AddHours(1) },
            new() { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), FromZone = Guid.NewGuid(), ToZone = Guid.NewGuid(), CreatedAt = startDate.AddHours(2) }
        };

        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(transitions);

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForTimeRange(startDate, endDate);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTransitionsForTimeRange_WhenNoTransitions_ReturnsEmptyList()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;

        _transitionRepository.Setup(r => r.List(It.IsAny<ISpecification<Transition>>()))
            .ReturnsAsync(new List<Transition>());

        var sut = CreateSut();

        // Act
        var result = await sut.GetTransitionsForTimeRange(startDate, endDate);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    #endregion
}
