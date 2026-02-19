namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class QueueServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<QueueService> _logger;
    private readonly QueueService _sut;

    public QueueServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUser = Substitute.For<ICurrentUserService>();
        _audit = Substitute.For<IAuditService>();
        _logger = Substitute.For<ILogger<QueueService>>();
        _sut = new QueueService(_context, _currentUser, _audit, _logger);

        _currentUser.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _currentUser.UserId.Returns("test-user-id");
    }

    public void Dispose() => _context.Dispose();

    private async Task<Company> CreateCompanyAsync(string name = "Test Co", string code = "TC")
    {
        var company = new Company { Name = name, Code = code };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        return company;
    }

    private async Task<Queue> CreateQueueAsync(Guid companyId, string name = "Support Queue",
        bool isDefault = false, bool isActive = true)
    {
        var queue = new Queue
        {
            CompanyId = companyId,
            Name = name,
            IsDefault = isDefault,
            IsActive = isActive,
        };
        _context.Queues.Add(queue);
        await _context.SaveChangesAsync();
        return queue;
    }

    [Fact]
    public async Task GetQueuesAsync_ValidCompanyId_ReturnsQueuesForCompany()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var otherCompany = await CreateCompanyAsync("Other Co", "OC");

        await CreateQueueAsync(company.Id, "Queue A");
        await CreateQueueAsync(company.Id, "Queue B");
        await CreateQueueAsync(otherCompany.Id, "Other Queue");

        // Act
        var result = await _sut.GetQueuesAsync(company.Id, 1, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(q => q.CompanyId == company.Id);
    }

    [Fact]
    public async Task GetQueuesAsync_NoAccess_ReturnsFailure()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        _currentUser.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.GetQueuesAsync(company.Id, 1, 10);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task GetQueueByIdAsync_ExistingQueue_ReturnsDto()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id, "Test Queue", isDefault: true);

        // Act
        var result = await _sut.GetQueueByIdAsync(queue.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(queue.Id);
        result.Value.Name.Should().Be("Test Queue");
        result.Value.IsDefault.Should().BeTrue();
        result.Value.CompanyId.Should().Be(company.Id);
    }

    [Fact]
    public async Task GetQueueByIdAsync_NotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetQueueByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task CreateQueueAsync_ValidRequest_CreatesQueue()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var request = new CreateQueueRequest(company.Id, "New Queue", "A test queue", false);

        // Act
        var result = await _sut.CreateQueueAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Queue");
        result.Value.Description.Should().Be("A test queue");
        result.Value.IsActive.Should().BeTrue();

        var inDb = await _context.Queues.FirstOrDefaultAsync(q => q.Name == "New Queue");
        inDb.Should().NotBeNull();

        await _audit.Received(1).LogAsync("Created", "Queue", Arg.Any<string>(),
            oldValues: Arg.Any<object?>(), newValues: Arg.Any<object?>(), ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateQueueAsync_DuplicateName_ReturnsFailure()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        await CreateQueueAsync(company.Id, "Support");

        var request = new CreateQueueRequest(company.Id, "Support", null, false);

        // Act
        var result = await _sut.CreateQueueAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateQueueAsync_SetDefault_UnsetsExistingDefault()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var firstQueue = await CreateQueueAsync(company.Id, "First Queue", isDefault: true);

        var request = new CreateQueueRequest(company.Id, "Second Queue", null, true);

        // Act
        var result = await _sut.CreateQueueAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsDefault.Should().BeTrue();

        // Verify first queue is no longer default
        var updatedFirst = await _context.Queues.FindAsync(firstQueue.Id);
        updatedFirst!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateQueueAsync_ValidRequest_UpdatesQueue()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id, "Original Name");
        var request = new UpdateQueueRequest("Updated Name", "Updated Description", false, true);

        // Act
        var result = await _sut.UpdateQueueAsync(queue.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Updated Name");
        result.Value.Description.Should().Be("Updated Description");

        var inDb = await _context.Queues.FindAsync(queue.Id);
        inDb!.Name.Should().Be("Updated Name");

        await _audit.Received(1).LogAsync("Updated", "Queue", Arg.Any<string>(),
            oldValues: Arg.Any<object?>(), newValues: Arg.Any<object?>(), ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateQueueAsync_SetDefault_UnsetsOtherDefault()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var firstQueue = await CreateQueueAsync(company.Id, "First Queue", isDefault: true);
        var secondQueue = await CreateQueueAsync(company.Id, "Second Queue", isDefault: false);

        var request = new UpdateQueueRequest("Second Queue", null, true, true);

        // Act
        var result = await _sut.UpdateQueueAsync(secondQueue.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsDefault.Should().BeTrue();

        // Verify first queue is no longer default
        // Need to re-query to avoid cached version
        _context.ChangeTracker.Clear();
        var updatedFirst = await _context.Queues.FindAsync(firstQueue.Id);
        updatedFirst!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteQueueAsync_NoTickets_SoftDeletes()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id, "Empty Queue");

        // Act
        var result = await _sut.DeleteQueueAsync(queue.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var deleted = await _context.Queues
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == queue.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
        deleted.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteQueueAsync_HasTickets_ReturnsFailure()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id, "Busy Queue");

        var ticket = new Ticket
        {
            CompanyId = company.Id,
            QueueId = queue.Id,
            TicketNumber = "TKT-TEST-0001",
            Subject = "Test Ticket",
            Description = "Test description",
            RequesterEmail = "test@test.com",
            RequesterName = "Test User",
            Source = TicketSource.WebForm,
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteQueueAsync(queue.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("tickets");
    }

    [Fact]
    public async Task GetQueuesAsync_CompanyIsolation_OnlyReturnsAccessibleCompanyQueues()
    {
        // Arrange
        var companyA = await CreateCompanyAsync("Company A", "CA");
        var companyB = await CreateCompanyAsync("Company B", "CB");

        await CreateQueueAsync(companyA.Id, "Queue A1");
        await CreateQueueAsync(companyA.Id, "Queue A2");
        await CreateQueueAsync(companyA.Id, "Queue A3");
        await CreateQueueAsync(companyB.Id, "Queue B1");
        await CreateQueueAsync(companyB.Id, "Queue B2");

        // User has access to Company A but not Company B
        _currentUser.HasAccessToCompanyAsync(companyA.Id, Arg.Any<CancellationToken>()).Returns(true);
        _currentUser.HasAccessToCompanyAsync(companyB.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var resultA = await _sut.GetQueuesAsync(companyA.Id, 1, 10);
        var resultB = await _sut.GetQueuesAsync(companyB.Id, 1, 10);

        // Assert
        resultA.IsSuccess.Should().BeTrue();
        resultA.Value!.Items.Should().HaveCount(3);

        resultB.IsSuccess.Should().BeFalse();
        resultB.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task GetQueuesAsync_PaginationWorks_ReturnsCorrectPage()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        await CreateQueueAsync(company.Id, "Alpha Queue");
        await CreateQueueAsync(company.Id, "Beta Queue");
        await CreateQueueAsync(company.Id, "Gamma Queue");

        // Act
        var result = await _sut.GetQueuesAsync(company.Id, page: 1, pageSize: 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.PageSize.Should().Be(2);
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public async Task CreateQueueAsync_AccessDenied_ReturnsFailure()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        _currentUser.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>()).Returns(false);

        var request = new CreateQueueRequest(company.Id, "New Queue", null, false);

        // Act
        var result = await _sut.CreateQueueAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task UpdateQueueAsync_NotFound_ReturnsFailure()
    {
        // Act
        var request = new UpdateQueueRequest("Name", null, false, true);
        var result = await _sut.UpdateQueueAsync(Guid.NewGuid(), request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task DeleteQueueAsync_NotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.DeleteQueueAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
}
