namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SupportHub.Application.Common;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;

public class AttachmentServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AttachmentService> _logger;
    private readonly AttachmentService _sut;

    public AttachmentServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _fileStorage = Substitute.For<IFileStorageService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<AttachmentService>>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:MaxFileSizeBytes"] = "26214400",
                ["FileStorage:AllowedExtensions"] = ".pdf,.doc,.docx,.txt,.png,.jpg"
            })
            .Build();

        _sut = new AttachmentService(_context, _fileStorage, _currentUserService, _configuration, _logger);

        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
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
    public async Task UploadAttachmentAsync_ValidFile_ReturnsAttachmentDto()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        _fileStorage.SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("2024/01/01/report.pdf"));

        using var stream = new MemoryStream(new byte[1024]);

        // Act
        var result = await _sut.UploadAttachmentAsync(
            ticket.Id, null, stream, "report.pdf", "application/pdf", 1024);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OriginalFileName.Should().Be("report.pdf");
        result.Value.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task UploadAttachmentAsync_ExceedsMaxSize_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        using var stream = new MemoryStream(new byte[100]);

        // Act
        var result = await _sut.UploadAttachmentAsync(
            ticket.Id, null, stream, "large.pdf", "application/pdf", 30_000_000);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().ContainAny("size", "MB");
    }

    [Fact]
    public async Task UploadAttachmentAsync_DisallowedExtension_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        using var stream = new MemoryStream(new byte[100]);

        // Act
        var result = await _sut.UploadAttachmentAsync(
            ticket.Id, null, stream, "virus.exe", "application/octet-stream", 1024);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("extension");
    }

    [Fact]
    public async Task UploadAttachmentAsync_WithMessageId_LinksToMessage()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        var message = new TicketMessage
        {
            TicketId = ticket.Id, Direction = MessageDirection.Inbound, Body = "See attached."
        };
        _context.TicketMessages.Add(message);
        await _context.SaveChangesAsync();

        _fileStorage.SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("2024/01/01/doc.pdf"));

        using var stream = new MemoryStream(new byte[100]);

        // Act
        var result = await _sut.UploadAttachmentAsync(
            ticket.Id, message.Id, stream, "doc.pdf", "application/pdf", 100);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketMessageId.Should().Be(message.Id);
    }

    [Fact]
    public async Task DownloadAttachmentAsync_ExistingAttachment_ReturnsStream()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        var attachment = new TicketAttachment
        {
            TicketId = ticket.Id,
            FileName = "report.pdf",
            OriginalFileName = "report.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            StoragePath = "2024/01/01/report.pdf"
        };
        _context.TicketAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        var fileStream = new MemoryStream(new byte[1024]);
        _fileStorage.GetFileAsync("2024/01/01/report.pdf", Arg.Any<CancellationToken>())
            .Returns(Result<Stream>.Success(fileStream));

        // Act
        var result = await _sut.DownloadAttachmentAsync(attachment.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.FileName.Should().Be("report.pdf");
    }

    [Fact]
    public async Task DownloadAttachmentAsync_NonExistentAttachment_ReturnsFailure()
    {
        // Act
        var result = await _sut.DownloadAttachmentAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetAttachmentsAsync_ReturnsAllForTicket()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        for (int i = 0; i < 3; i++)
        {
            _context.TicketAttachments.Add(new TicketAttachment
            {
                TicketId = ticket.Id,
                FileName = $"file{i}.pdf",
                OriginalFileName = $"file{i}.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                StoragePath = $"2024/01/01/file{i}.pdf"
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAttachmentsAsync(ticket.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(3);
    }
}
