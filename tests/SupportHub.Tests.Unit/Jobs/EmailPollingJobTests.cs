namespace SupportHub.Tests.Unit.Jobs;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SupportHub.Application.Common;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Jobs;
using SupportHub.Tests.Unit.Helpers;

public class EmailPollingJobTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly IEmailPollingService _emailPollingService;
    private readonly ILogger<EmailPollingJob> _logger;
    private readonly EmailPollingJob _sut;

    private readonly Company _company;

    public EmailPollingJobTests()
    {
        _context = TestDbContextFactory.Create();
        _emailPollingService = Substitute.For<IEmailPollingService>();
        _logger = Substitute.For<ILogger<EmailPollingJob>>();

        _sut = new EmailPollingJob(_context, _emailPollingService, _logger);

        _company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(_company);
        _context.SaveChanges();

        _emailPollingService.PollMailboxAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ExecuteAsync_SkipsInactiveConfigurations()
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
        await _sut.ExecuteAsync();

        // Assert
        await _emailPollingService.DidNotReceive()
            .PollMailboxAsync(config.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesActiveConfiguration()
    {
        // Arrange
        var config = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = true,
            LastPolledAt = null, // never polled — due immediately
            PollingIntervalMinutes = 2,
        };
        _context.EmailConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Act
        await _sut.ExecuteAsync();

        // Assert
        await _emailPollingService.Received(1)
            .PollMailboxAsync(config.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsConfigNotYetDue()
    {
        // Arrange — just polled (LastPolledAt = now)
        var config = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = true,
            LastPolledAt = DateTimeOffset.UtcNow,
            PollingIntervalMinutes = 2,
        };
        _context.EmailConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Act
        await _sut.ExecuteAsync();

        // Assert
        await _emailPollingService.DidNotReceive()
            .PollMailboxAsync(config.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OneConfigFailure_DoesNotBlockOthers()
    {
        // Arrange
        var config1 = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "fail@test.com",
            DisplayName = "Failing Config",
            IsActive = true,
            LastPolledAt = null,
            PollingIntervalMinutes = 2,
        };
        var config2 = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "ok@test.com",
            DisplayName = "OK Config",
            IsActive = true,
            LastPolledAt = null,
            PollingIntervalMinutes = 2,
        };
        _context.EmailConfigurations.AddRange(config1, config2);
        await _context.SaveChangesAsync();

        _emailPollingService.PollMailboxAsync(config1.Id, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Failure("Connection failed"));
        _emailPollingService.PollMailboxAsync(config2.Id, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(5));

        // Act
        await _sut.ExecuteAsync();

        // Assert — both were attempted
        await _emailPollingService.Received(1).PollMailboxAsync(config1.Id, Arg.Any<CancellationToken>());
        await _emailPollingService.Received(1).PollMailboxAsync(config2.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesDueConfiguration_OldLastPolledAt()
    {
        // Arrange — polled 10 minutes ago with 2-minute interval → due
        var config = new EmailConfiguration
        {
            CompanyId = _company.Id,
            SharedMailboxAddress = "support@test.com",
            DisplayName = "Test Support",
            IsActive = true,
            LastPolledAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            PollingIntervalMinutes = 2,
        };
        _context.EmailConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Act
        await _sut.ExecuteAsync();

        // Assert
        await _emailPollingService.Received(1)
            .PollMailboxAsync(config.Id, Arg.Any<CancellationToken>());
    }
}
