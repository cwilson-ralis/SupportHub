namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class EmailPollingServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly IGraphClientFactory _graphClientFactory;
    private readonly IEmailProcessingService _emailProcessingService;
    private readonly ILogger<EmailPollingService> _logger;
    private readonly EmailPollingService _sut;

    private readonly Company _company;

    public EmailPollingServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _graphClientFactory = Substitute.For<IGraphClientFactory>();
        _emailProcessingService = Substitute.For<IEmailProcessingService>();
        _logger = Substitute.For<ILogger<EmailPollingService>>();

        _sut = new EmailPollingService(
            _context, _graphClientFactory, _emailProcessingService, _logger);

        _company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(_company);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task PollMailboxAsync_ConfigNotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.PollMailboxAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Email configuration not found");
    }

    [Fact]
    public async Task PollMailboxAsync_InactiveConfig_ReturnsFailure()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = false,
        };
        _context.EmailConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.PollMailboxAsync(config.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactive");
    }

    [Fact]
    public async Task PollMailboxAsync_ActiveConfig_GraphReturnsEmpty_ReturnsSuccessWithZero()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = true,
            LastPolledAt = null,
        };
        _context.EmailConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Create a real GraphServiceClient with a substitute adapter.
        // The adapter returns null for SendAsync by default, so GetAsync returns null.
        // The service handles null: messagesPage?.Value ?? [] → empty list.
        var mockAdapter = Substitute.For<Microsoft.Kiota.Abstractions.IRequestAdapter>();
        mockAdapter.BaseUrl.Returns("https://graph.microsoft.com/v1.0");
        var graphClient = new Microsoft.Graph.GraphServiceClient(mockAdapter);
        _graphClientFactory.CreateClient().Returns(graphClient);

        // Act
        var result = await _sut.PollMailboxAsync(config.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);

        // Verify LastPolledAt was updated
        var updated = await _context.EmailConfigurations.FindAsync(config.Id);
        updated!.LastPolledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PollMailboxAsync_SoftDeletedConfig_ReturnsFailure()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = true,
            IsDeleted = true,
            DeletedAt = DateTimeOffset.UtcNow,
        };
        _context.EmailConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.PollMailboxAsync(config.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Email configuration not found");
    }
}
