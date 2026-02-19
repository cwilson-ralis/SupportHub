namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;

public class InternalNoteServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InternalNoteService> _logger;
    private readonly InternalNoteService _sut;

    public InternalNoteServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<InternalNoteService>>();
        _sut = new InternalNoteService(_context, _currentUserService, _logger);

        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>
            {
                new() { CompanyId = Guid.NewGuid(), Role = UserRole.Agent, UserId = userId }
            }));
    }

    public void Dispose() => _context.Dispose();

    private async Task<(Company company, Ticket ticket)> CreateTestTicketAsync()
    {
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);

        var ticket = new Ticket
        {
            CompanyId = company.Id,
            TicketNumber = "TKT-20260218-0001",
            Subject = "Test Subject",
            Description = "Test Description",
            Priority = TicketPriority.Medium,
            Source = TicketSource.WebForm,
            RequesterEmail = "req@test.com",
            RequesterName = "Requester"
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        return (company, ticket);
    }

    [Fact]
    public async Task AddNoteAsync_AgentUser_ReturnsNoteDto()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        // Create application user so author can be found
        var userId = Guid.Parse(_currentUserService.UserId!);
        _context.ApplicationUsers.Add(new ApplicationUser
        {
            Id = userId, Email = "agent@test.com", DisplayName = "Test Agent", AzureAdObjectId = "aad-123"
        });
        await _context.SaveChangesAsync();

        var request = new CreateInternalNoteRequest("This is an internal note.");

        // Act
        var result = await _sut.AddNoteAsync(ticket.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().Be("This is an internal note.");
    }

    [Fact]
    public async Task AddNoteAsync_NoRoles_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        _currentUserService.GetUserRolesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserCompanyRole>>(new List<UserCompanyRole>()));

        var request = new CreateInternalNoteRequest("Should fail.");

        // Act
        var result = await _sut.AddNoteAsync(ticket.Id, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task AddNoteAsync_SetsAuthorIdFromCurrentUser()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        var userId = Guid.Parse(_currentUserService.UserId!);
        _context.ApplicationUsers.Add(new ApplicationUser
        {
            Id = userId, Email = "agent@test.com", DisplayName = "Test Agent", AzureAdObjectId = "aad-456"
        });
        await _context.SaveChangesAsync();

        var request = new CreateInternalNoteRequest("Note with author.");

        // Act
        var result = await _sut.AddNoteAsync(ticket.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AuthorId.Should().Be(userId);
    }

    [Fact]
    public async Task GetNotesAsync_ReturnsOrderedByCreatedAt()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        var authorId = Guid.NewGuid();
        _context.ApplicationUsers.Add(new ApplicationUser
        {
            Id = authorId, Email = "agent@test.com", DisplayName = "Agent", AzureAdObjectId = "aad-789"
        });

        var note1 = new InternalNote
        {
            TicketId = ticket.Id, AuthorId = authorId, Body = "First",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };
        var note2 = new InternalNote
        {
            TicketId = ticket.Id, AuthorId = authorId, Body = "Second",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        };
        var note3 = new InternalNote
        {
            TicketId = ticket.Id, AuthorId = authorId, Body = "Third",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        _context.InternalNotes.AddRange(note3, note1, note2); // out of order
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetNotesAsync(ticket.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
        result.Value![0].Body.Should().Be("First");
        result.Value![1].Body.Should().Be("Second");
        result.Value![2].Body.Should().Be("Third");
    }

    [Fact]
    public async Task GetNotesAsync_NonExistentTicket_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetNotesAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
}
