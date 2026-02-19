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

public class EmailProcessingServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ITicketService _ticketService;
    private readonly ITicketMessageService _ticketMessageService;
    private readonly IAttachmentService _attachmentService;
    private readonly IAiClassificationService _aiClassificationService;
    private readonly ILogger<EmailProcessingService> _logger;
    private readonly EmailProcessingService _sut;

    private readonly Company _company;
    private readonly EmailConfiguration _emailConfig;

    public EmailProcessingServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _ticketService = Substitute.For<ITicketService>();
        _ticketMessageService = Substitute.For<ITicketMessageService>();
        _attachmentService = Substitute.For<IAttachmentService>();
        _aiClassificationService = Substitute.For<IAiClassificationService>();
        _logger = Substitute.For<ILogger<EmailProcessingService>>();

        _sut = new EmailProcessingService(
            _context, _ticketService, _ticketMessageService,
            _attachmentService, _aiClassificationService, _logger);

        _company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(_company);
        _context.SaveChanges();

        _emailConfig = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = true,
            AutoCreateTickets = true,
            DefaultPriority = TicketPriority.Medium,
        };
        _context.EmailConfigurations.Add(_emailConfig);
        _context.SaveChanges();

        // Default mock: AI classification returns success with no-op result
        _aiClassificationService.ClassifyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result<AiClassificationResult>.Success(
                new AiClassificationResult(null, [], null, 0, "none", string.Empty)));
    }

    public void Dispose() => _context.Dispose();

    private static InboundEmailMessage BuildEmail(
        string externalId = "msg-001",
        string subject = "Test Subject",
        IReadOnlyDictionary<string, string>? headers = null,
        IReadOnlyList<EmailAttachment>? attachments = null) => new(
        ExternalMessageId: externalId,
        Subject: subject,
        Body: "Test body",
        HtmlBody: null,
        SenderEmail: "user@example.com",
        SenderName: "Test User",
        ReceivedAt: DateTimeOffset.UtcNow,
        Attachments: attachments ?? [],
        InternetHeaders: headers ?? new Dictionary<string, string>());

    [Fact]
    public async Task ProcessInboundEmailAsync_DuplicateExternalMessageId_SkipsProcessing()
    {
        // Arrange — seed an existing log with the same ExternalMessageId
        _context.EmailProcessingLogs.Add(new EmailProcessingLog
        {
            EmailConfigurationId = _emailConfig.Id,
            ExternalMessageId = "msg-dup",
            ProcessingResult = "Created",
            ProcessedAt = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        var email = BuildEmail(externalId: "msg-dup");

        // Act
        var result = await _sut.ProcessInboundEmailAsync(email, _emailConfig.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull(); // skipped
        await _ticketService.DidNotReceive().CreateTicketAsync(Arg.Any<CreateTicketRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessInboundEmailAsync_NoMatch_CreatesNewTicket()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketService.CreateTicketAsync(Arg.Any<CreateTicketRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TicketDto>.Success(new TicketDto(
                ticketId, _company.Id, _company.Name, "TKT-20260218-0001", "Test Subject", "Test body",
                TicketStatus.New, TicketPriority.Medium, TicketSource.Email,
                "user@example.com", "Test User", null, null, null, null,
                null, null, null, null, [], [], DateTimeOffset.UtcNow, null)));

        // Seed a ticket entity so AI classification storage can find it
        _context.Tickets.Add(new Ticket
        {
            Id = ticketId,
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0001",
            Subject = "Test Subject",
            Description = "Test body",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        });
        await _context.SaveChangesAsync();

        var email = BuildEmail(externalId: "msg-new");

        // Act
        var result = await _sut.ProcessInboundEmailAsync(email, _emailConfig.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ticketId);
        await _ticketService.Received(1).CreateTicketAsync(Arg.Any<CreateTicketRequest>(), Arg.Any<CancellationToken>());

        var log = await _context.EmailProcessingLogs.FirstOrDefaultAsync(l => l.ExternalMessageId == "msg-new");
        log.Should().NotBeNull();
        log!.ProcessingResult.Should().Be("Created");
    }

    [Fact]
    public async Task ProcessInboundEmailAsync_MatchesByHeader_AppendsToExistingTicket()
    {
        // Arrange — existing ticket
        var existingTicket = new Ticket
        {
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0001",
            Subject = "Existing",
            Description = "Existing desc",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        };
        _context.Tickets.Add(existingTicket);
        await _context.SaveChangesAsync();

        var messageDto = new TicketMessageDto(
            Guid.NewGuid(), existingTicket.Id, MessageDirection.Inbound,
            "user@example.com", "Test User", "Test body", null, [], DateTimeOffset.UtcNow);
        _ticketMessageService.AddMessageAsync(existingTicket.Id, Arg.Any<CreateTicketMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TicketMessageDto>.Success(messageDto));

        var headers = new Dictionary<string, string>
        {
            ["X-SupportHub-TicketId"] = existingTicket.Id.ToString()
        };
        var email = BuildEmail(externalId: "msg-header", headers: headers);

        // Act
        var result = await _sut.ProcessInboundEmailAsync(email, _emailConfig.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingTicket.Id);
        await _ticketMessageService.Received(1).AddMessageAsync(existingTicket.Id, Arg.Any<CreateTicketMessageRequest>(), Arg.Any<CancellationToken>());

        var log = await _context.EmailProcessingLogs.FirstOrDefaultAsync(l => l.ExternalMessageId == "msg-header");
        log.Should().NotBeNull();
        log!.ProcessingResult.Should().Be("Appended");
    }

    [Fact]
    public async Task ProcessInboundEmailAsync_MatchesBySubjectFallback_AppendsToExistingTicket()
    {
        // Arrange
        var existingTicket = new Ticket
        {
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0001",
            Subject = "Existing",
            Description = "Existing desc",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        };
        _context.Tickets.Add(existingTicket);
        await _context.SaveChangesAsync();

        var messageDto = new TicketMessageDto(
            Guid.NewGuid(), existingTicket.Id, MessageDirection.Inbound,
            "user@example.com", "Test User", "Test body", null, [], DateTimeOffset.UtcNow);
        _ticketMessageService.AddMessageAsync(existingTicket.Id, Arg.Any<CreateTicketMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TicketMessageDto>.Success(messageDto));

        // Subject contains the ticket number pattern
        var email = BuildEmail(
            externalId: "msg-subject",
            subject: "Re: [TKT-20260218-0001] Existing");

        // Act
        var result = await _sut.ProcessInboundEmailAsync(email, _emailConfig.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingTicket.Id);
        await _ticketMessageService.Received(1).AddMessageAsync(existingTicket.Id, Arg.Any<CreateTicketMessageRequest>(), Arg.Any<CancellationToken>());

        var log = await _context.EmailProcessingLogs.FirstOrDefaultAsync(l => l.ExternalMessageId == "msg-subject");
        log!.ProcessingResult.Should().Be("Appended");
    }

    [Fact]
    public async Task ProcessInboundEmailAsync_AutoCreateTicketsFalse_SkipsWhenNoMatch()
    {
        // Arrange — disable auto-create
        _emailConfig.AutoCreateTickets = false;
        await _context.SaveChangesAsync();

        var email = BuildEmail(externalId: "msg-skip");

        // Act
        var result = await _sut.ProcessInboundEmailAsync(email, _emailConfig.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull(); // no ticket created
        await _ticketService.DidNotReceive().CreateTicketAsync(Arg.Any<CreateTicketRequest>(), Arg.Any<CancellationToken>());

        var log = await _context.EmailProcessingLogs.FirstOrDefaultAsync(l => l.ExternalMessageId == "msg-skip");
        log.Should().NotBeNull();
        log!.ProcessingResult.Should().Be("Skipped");
    }

    [Fact]
    public async Task ProcessInboundEmailAsync_StoresAiClassificationResult()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        _ticketService.CreateTicketAsync(Arg.Any<CreateTicketRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TicketDto>.Success(new TicketDto(
                ticketId, _company.Id, _company.Name, "TKT-20260218-0002", "AI Test", "Body",
                TicketStatus.New, TicketPriority.Medium, TicketSource.Email,
                "user@example.com", "Test User", null, null, null, null,
                null, null, null, null, [], [], DateTimeOffset.UtcNow, null)));

        _aiClassificationService.ClassifyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result<AiClassificationResult>.Success(
                new AiClassificationResult("IT Support", ["hardware", "laptop"], "Hardware Issue", 0.85, "gpt-4", "{}")));

        // Seed the ticket entity so the service can find it to store AI classification
        _context.Tickets.Add(new Ticket
        {
            Id = ticketId,
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0002",
            Subject = "AI Test",
            Description = "Body",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        });
        await _context.SaveChangesAsync();

        var email = BuildEmail(externalId: "msg-ai");

        // Act
        var result = await _sut.ProcessInboundEmailAsync(email, _emailConfig.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ticketId);

        var ticket = await _context.Tickets.FindAsync(ticketId);
        ticket!.AiClassification.Should().NotBeNullOrEmpty();
        ticket.AiClassification.Should().Contain("IT Support");
    }

    [Fact]
    public async Task ProcessInboundEmailAsync_WithAttachments_SavesViaAttachmentService()
    {
        // Arrange
        var existingTicket = new Ticket
        {
            CompanyId = _company.Id,
            TicketNumber = "TKT-20260218-0003",
            Subject = "Attachment Test",
            Description = "Desc",
            RequesterEmail = "user@example.com",
            RequesterName = "Test User",
            Source = TicketSource.Email,
        };
        _context.Tickets.Add(existingTicket);
        await _context.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var messageDto = new TicketMessageDto(
            messageId, existingTicket.Id, MessageDirection.Inbound,
            "user@example.com", "Test User", "Test body", null, [], DateTimeOffset.UtcNow);
        _ticketMessageService.AddMessageAsync(existingTicket.Id, Arg.Any<CreateTicketMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TicketMessageDto>.Success(messageDto));

        _attachmentService.UploadAttachmentAsync(
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Stream>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result<TicketAttachmentDto>.Success(
                new TicketAttachmentDto(Guid.NewGuid(), existingTicket.Id, messageId, "stored.pdf", "test.pdf", "application/pdf", 1024, DateTimeOffset.UtcNow)));

        var attachments = new List<EmailAttachment>
        {
            new("test.pdf", "application/pdf", 1024, new MemoryStream(new byte[1024]))
        };

        var headers = new Dictionary<string, string>
        {
            ["X-SupportHub-TicketId"] = existingTicket.Id.ToString()
        };
        var email = BuildEmail(externalId: "msg-attach", headers: headers, attachments: attachments);

        // Act
        var result = await _sut.ProcessInboundEmailAsync(email, _emailConfig.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _attachmentService.Received(1).UploadAttachmentAsync(
            existingTicket.Id, messageId, Arg.Any<Stream>(),
            "test.pdf", "application/pdf", 1024, Arg.Any<CancellationToken>());
    }
}
