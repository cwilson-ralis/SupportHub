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

public class TicketMessageServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TicketMessageService> _logger;
    private readonly TicketMessageService _sut;

    public TicketMessageServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<TicketMessageService>>();
        _sut = new TicketMessageService(_context, _currentUserService, _logger);

        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    public void Dispose() => _context.Dispose();

    private async Task<(Company company, Ticket ticket)> CreateTestTicketAsync(TicketStatus status = TicketStatus.New)
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
            RequesterName = "Requester",
            Status = status
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        return (company, ticket);
    }

    [Fact]
    public async Task AddMessageAsync_ValidRequest_ReturnsMessageDto()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        var request = new CreateTicketMessageRequest(
            MessageDirection.Inbound, "sender@test.com", "Sender", "Hello, I need help.", null);

        // Act
        var result = await _sut.AddMessageAsync(ticket.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().Be("Hello, I need help.");
        result.Value.Direction.Should().Be(MessageDirection.Inbound);
    }

    [Fact]
    public async Task AddMessageAsync_FirstOutboundMessage_SetsFirstResponseAt()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        var request = new CreateTicketMessageRequest(
            MessageDirection.Outbound, "agent@test.com", "Agent", "We're looking into this.", null);

        // Act
        await _sut.AddMessageAsync(ticket.Id, request);

        // Assert
        var updated = await _context.Tickets.FindAsync(ticket.Id);
        updated!.FirstResponseAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AddMessageAsync_SubsequentOutbound_DoesNotOverwriteFirstResponseAt()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        var request1 = new CreateTicketMessageRequest(
            MessageDirection.Outbound, "agent@test.com", "Agent", "First response", null);
        await _sut.AddMessageAsync(ticket.Id, request1);

        var firstTicket = await _context.Tickets.FindAsync(ticket.Id);
        var originalFirstResponseAt = firstTicket!.FirstResponseAt;

        // Small delay to ensure timestamps differ
        await Task.Delay(10);

        var request2 = new CreateTicketMessageRequest(
            MessageDirection.Outbound, "agent@test.com", "Agent", "Second response", null);

        // Act
        await _sut.AddMessageAsync(ticket.Id, request2);

        // Assert
        var updated = await _context.Tickets.FindAsync(ticket.Id);
        updated!.FirstResponseAt.Should().Be(originalFirstResponseAt);
    }

    [Fact]
    public async Task AddMessageAsync_OutboundOnNewTicket_TransitionsToOpen()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync(TicketStatus.New);
        var request = new CreateTicketMessageRequest(
            MessageDirection.Outbound, "agent@test.com", "Agent", "On it!", null);

        // Act
        await _sut.AddMessageAsync(ticket.Id, request);

        // Assert
        var updated = await _context.Tickets.FindAsync(ticket.Id);
        updated!.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task AddMessageAsync_NonExistentTicket_ReturnsFailure()
    {
        // Arrange
        var request = new CreateTicketMessageRequest(
            MessageDirection.Inbound, "sender@test.com", "Sender", "Hello", null);

        // Act
        var result = await _sut.AddMessageAsync(Guid.NewGuid(), request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsOrderedByCreatedAt()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        var msg1 = new TicketMessage
        {
            TicketId = ticket.Id, Direction = MessageDirection.Inbound,
            Body = "First", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };
        var msg2 = new TicketMessage
        {
            TicketId = ticket.Id, Direction = MessageDirection.Outbound,
            Body = "Second", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        };
        var msg3 = new TicketMessage
        {
            TicketId = ticket.Id, Direction = MessageDirection.Inbound,
            Body = "Third", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        _context.TicketMessages.AddRange(msg3, msg1, msg2); // add out of order
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetMessagesAsync(ticket.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
        result.Value![0].Body.Should().Be("First");
        result.Value![1].Body.Should().Be("Second");
        result.Value![2].Body.Should().Be("Third");
    }
}
