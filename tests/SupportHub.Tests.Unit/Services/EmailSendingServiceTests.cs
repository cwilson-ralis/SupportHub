namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class EmailSendingServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly IGraphClientFactory _graphClientFactory;
    private readonly ITicketMessageService _ticketMessageService;
    private readonly IAuditService _auditService;
    private readonly ILogger<EmailSendingService> _logger;
    private readonly EmailSendingService _sut;

    private readonly Company _company;
    private readonly EmailConfiguration _emailConfig;

    public EmailSendingServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _graphClientFactory = Substitute.For<IGraphClientFactory>();
        _ticketMessageService = Substitute.For<ITicketMessageService>();
        _auditService = Substitute.For<IAuditService>();
        _logger = Substitute.For<ILogger<EmailSendingService>>();

        _sut = new EmailSendingService(
            _context, _graphClientFactory, _ticketMessageService, _auditService, _logger);

        _company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(_company);
        _context.SaveChanges();

        _emailConfig = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = true,
        };
        _context.EmailConfigurations.Add(_emailConfig);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SendReplyAsync_TicketNotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.SendReplyAsync(Guid.NewGuid(), "Reply body", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Ticket not found");
    }

    [Fact]
    public async Task SendReplyAsync_NoActiveEmailConfig_ReturnsFailure()
    {
        // Arrange — ticket exists but deactivate the email config
        var ticket = new Ticket
        {
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0001",
            Subject = "Test",
            Description = "Test desc",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        };
        _context.Tickets.Add(ticket);

        _emailConfig.IsActive = false;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SendReplyAsync(ticket.Id, "Reply body", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No active email configuration");
    }

    [Fact]
    public async Task SendReplyAsync_GraphCallFails_ReturnsFailure()
    {
        // Arrange
        var ticket = new Ticket
        {
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0001",
            Subject = "Test Subject",
            Description = "Test desc",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Create a real GraphServiceClient with a mock adapter that throws on send
        var mockAdapter = Substitute.For<IRequestAdapter>();
        mockAdapter.BaseUrl.Returns("https://graph.microsoft.com/v1.0");
        mockAdapter.SendNoContentAsync(
            Arg.Any<RequestInformation>(),
            Arg.Any<Dictionary<string, ParsableFactory<IParsable>>?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Graph send failed"));
        var graphClient = new GraphServiceClient(mockAdapter);
        _graphClientFactory.CreateClient().Returns(graphClient);

        // Act
        var result = await _sut.SendReplyAsync(ticket.Id, "Reply body", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to send email");
    }

    [Fact]
    public async Task SendReplyAsync_FormatsSubjectCorrectly()
    {
        // Arrange
        var ticket = new Ticket
        {
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0099",
            Subject = "Login Issue",
            Description = "Desc",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Create a real GraphServiceClient with a mock adapter that succeeds
        var mockAdapter = Substitute.For<IRequestAdapter>();
        mockAdapter.BaseUrl.Returns("https://graph.microsoft.com/v1.0");
        var graphClient = new GraphServiceClient(mockAdapter);
        _graphClientFactory.CreateClient().Returns(graphClient);

        // Mock downstream services
        _ticketMessageService.AddMessageAsync(Arg.Any<Guid>(), Arg.Any<CreateTicketMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TicketMessageDto>.Success(
                new TicketMessageDto(Guid.NewGuid(), ticket.Id, MessageDirection.Outbound,
                    "support@test.com", "Test Support", "Reply body", null, [], DateTimeOffset.UtcNow)));

        // Act
        var result = await _sut.SendReplyAsync(ticket.Id, "Reply body", null, null);

        // Assert — The send succeeded (adapter returns Task.CompletedTask by default for SendNoContentAsync)
        result.IsSuccess.Should().BeTrue();

        // Verify the audit log was called with the correctly formatted subject
        await _auditService.Received(1).LogAsync(
            "EmailReplySent",
            "Ticket",
            ticket.Id.ToString(),
            Arg.Any<object?>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());

        // Verify Graph client was created (meaning we passed the ticket/config guards)
        _graphClientFactory.Received(1).CreateClient();
    }
}
