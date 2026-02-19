namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class TicketServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly IRoutingEngine _routingEngine;
    private readonly ILogger<TicketService> _logger;
    private readonly TicketService _sut;

    public TicketServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _auditService = Substitute.For<IAuditService>();
        _routingEngine = Substitute.For<IRoutingEngine>();
        _logger = Substitute.For<ILogger<TicketService>>();
        _sut = new TicketService(_context, _currentUserService, _auditService, _routingEngine, _logger);

        _routingEngine.EvaluateAsync(Arg.Any<RoutingContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<RoutingResult>.Success(new RoutingResult(
                QueueId: null,
                QueueName: null,
                AutoAssignAgentId: null,
                AutoSetPriority: null,
                AutoAddTags: [],
                MatchedRuleId: null,
                MatchedRuleName: null,
                IsDefaultFallback: false))));

        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _currentUserService.UserId.Returns("test-user-id");
        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>()));
    }

    public void Dispose() => _context.Dispose();

    private async Task<(Company company, Ticket ticket)> CreateTestTicketAsync()
    {
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        _currentUserService.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>()).Returns(true);
        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>
            {
                new() { CompanyId = company.Id, Role = UserRole.Agent }
            }));

        var request = new CreateTicketRequest(company.Id, "Test Subject", "Test Description",
            TicketPriority.Medium, TicketSource.WebForm, "req@test.com", "Requester", null, null, null);
        var result = await _sut.CreateTicketAsync(request);

        var ticket = await _context.Tickets.FindAsync(result.Value!.Id);
        return (company, ticket!);
    }

    [Fact]
    public async Task CreateTicketAsync_ValidRequest_ReturnsTicketWithGeneratedTicketNumber()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        var request = new CreateTicketRequest(company.Id, "Test Subject", "Test Description",
            TicketPriority.Medium, TicketSource.WebForm, "req@test.com", "Requester", null, null, null);

        // Act
        var result = await _sut.CreateTicketAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        result.Value!.TicketNumber.Should().MatchRegex($@"TKT-{today}-\d{{4}}");
    }

    [Fact]
    public async Task CreateTicketAsync_ValidRequest_SetsStatusToNew()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        var request = new CreateTicketRequest(company.Id, "Test Subject", "Test Description",
            TicketPriority.Medium, TicketSource.WebForm, "req@test.com", "Requester", null, null, null);

        // Act
        var result = await _sut.CreateTicketAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(TicketStatus.New);
    }

    [Fact]
    public async Task CreateTicketAsync_WithTags_CreatesTags()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        var request = new CreateTicketRequest(company.Id, "Test Subject", "Test Description",
            TicketPriority.Medium, TicketSource.WebForm, "req@test.com", "Requester", null, null,
            new List<string> { "bug", "urgent" });

        // Act
        var result = await _sut.CreateTicketAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateTicketAsync_AccessDenied_ReturnsFailure()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        _currentUserService.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>()).Returns(false);

        var request = new CreateTicketRequest(company.Id, "Test Subject", "Test Description",
            TicketPriority.Medium, TicketSource.WebForm, "req@test.com", "Requester", null, null, null);

        // Act
        var result = await _sut.CreateTicketAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task GetTicketByIdAsync_ExistingTicket_ReturnsDto()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        // Act
        var result = await _sut.GetTicketByIdAsync(ticket.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketNumber.Should().Be(ticket.TicketNumber);
    }

    [Fact]
    public async Task GetTicketByIdAsync_NonExistentTicket_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetTicketByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetTicketByIdAsync_AccessDenied_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        _currentUserService.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _sut.GetTicketByIdAsync(ticket.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task GetTicketsAsync_ReturnsOnlyAccessibleCompanyTickets()
    {
        // Arrange
        var companyA = new Company { Name = "Company A", Code = "CA" };
        var companyB = new Company { Name = "Company B", Code = "CB" };
        _context.Companies.AddRange(companyA, companyB);
        await _context.SaveChangesAsync();

        _context.Tickets.Add(new Ticket
        {
            CompanyId = companyA.Id, TicketNumber = "TKT-A-0001", Subject = "Ticket A",
            Description = "Desc A", RequesterEmail = "a@test.com", RequesterName = "A"
        });
        _context.Tickets.Add(new Ticket
        {
            CompanyId = companyB.Id, TicketNumber = "TKT-B-0001", Subject = "Ticket B",
            Description = "Desc B", RequesterEmail = "b@test.com", RequesterName = "B"
        });
        await _context.SaveChangesAsync();

        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>
            {
                new() { CompanyId = companyA.Id, Role = UserRole.Agent }
            }));

        var filter = new TicketFilterRequest(null, null, null, null, null, null, null, null, 1, 10);

        // Act
        var result = await _sut.GetTicketsAsync(filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().OnlyContain(t => t.CompanyName == "Company A");
    }

    [Fact]
    public async Task GetTicketsAsync_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        _context.Tickets.Add(new Ticket
        {
            CompanyId = company.Id, TicketNumber = "TKT-1", Subject = "New Ticket",
            Description = "D", RequesterEmail = "a@t.com", RequesterName = "A",
            Status = TicketStatus.New
        });
        _context.Tickets.Add(new Ticket
        {
            CompanyId = company.Id, TicketNumber = "TKT-2", Subject = "Open Ticket",
            Description = "D", RequesterEmail = "b@t.com", RequesterName = "B",
            Status = TicketStatus.Open
        });
        await _context.SaveChangesAsync();

        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>
            {
                new() { CompanyId = company.Id, Role = UserRole.Agent }
            }));

        var filter = new TicketFilterRequest(null, TicketStatus.Open, null, null, null, null, null, null, 1, 10);

        // Act
        var result = await _sut.GetTicketsAsync(filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.First().Subject.Should().Be("Open Ticket");
    }

    [Fact]
    public async Task GetTicketsAsync_WithSearchTerm_MatchesSubject()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        _context.Tickets.Add(new Ticket
        {
            CompanyId = company.Id, TicketNumber = "TKT-1", Subject = "Login Issue",
            Description = "D", RequesterEmail = "a@t.com", RequesterName = "A"
        });
        _context.Tickets.Add(new Ticket
        {
            CompanyId = company.Id, TicketNumber = "TKT-2", Subject = "Password Reset",
            Description = "D", RequesterEmail = "b@t.com", RequesterName = "B"
        });
        await _context.SaveChangesAsync();

        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>
            {
                new() { CompanyId = company.Id, Role = UserRole.Agent }
            }));

        var filter = new TicketFilterRequest(null, null, null, null, "Login", null, null, null, 1, 10);

        // Act
        var result = await _sut.GetTicketsAsync(filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.First().Subject.Should().Be("Login Issue");
    }

    [Fact]
    public async Task UpdateTicketAsync_ValidRequest_UpdatesSubject()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        var request = new UpdateTicketRequest("Updated Subject", null, null, null, null);

        // Act
        var result = await _sut.UpdateTicketAsync(ticket.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Updated Subject");
    }

    [Fact]
    public async Task ChangeStatusAsync_ValidTransition_UpdatesStatus()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        // Ticket starts as New; New -> Open is valid

        // Act
        var result = await _sut.ChangeStatusAsync(ticket.Id, TicketStatus.Open);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _context.Tickets.FindAsync(ticket.Id);
        updated!.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task ChangeStatusAsync_InvalidTransition_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        // Ticket starts as New; New -> Resolved is NOT valid

        // Act
        var result = await _sut.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid");
    }

    [Fact]
    public async Task ChangeStatusAsync_ToResolved_SetsResolvedAt()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        // New -> Open first
        await _sut.ChangeStatusAsync(ticket.Id, TicketStatus.Open);

        // Act — Open -> Resolved
        var result = await _sut.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _context.Tickets.FindAsync(ticket.Id);
        updated!.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ChangeStatusAsync_ReopenFromResolved_ClearsResolvedAt()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        await _sut.ChangeStatusAsync(ticket.Id, TicketStatus.Open);
        await _sut.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved);

        // Verify ResolvedAt is set
        var resolved = await _context.Tickets.FindAsync(ticket.Id);
        resolved!.ResolvedAt.Should().NotBeNull();

        // Act — Resolved -> Open
        var result = await _sut.ChangeStatusAsync(ticket.Id, TicketStatus.Open);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _context.Tickets.FindAsync(ticket.Id);
        updated!.ResolvedAt.Should().BeNull();
        updated.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTicketAsync_ExistingTicket_SoftDeletes()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        // Act
        var result = await _sut.DeleteTicketAsync(ticket.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = await _context.Tickets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == ticket.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
        deleted.DeletedAt.Should().NotBeNull();
    }
}
