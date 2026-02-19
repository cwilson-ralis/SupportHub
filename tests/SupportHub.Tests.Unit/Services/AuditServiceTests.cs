namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using NSubstitute;
using SupportHub.Application.Interfaces;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class AuditServiceTests : IDisposable
{
    private readonly SupportHub.Infrastructure.Data.SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly AuditService _sut;

    public AuditServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("user-123");
        _currentUserService.DisplayName.Returns("Test User");
        _sut = new AuditService(_context, _currentUserService);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task LogAsync_CreatesAuditLogEntry()
    {
        // Act
        await _sut.LogAsync("Create", "Company", "company-id-1");

        // Assert
        _context.AuditLogEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task LogAsync_PopulatesUserInfoFromCurrentUserService()
    {
        // Act
        await _sut.LogAsync("Update", "Ticket", "ticket-id-1");

        // Assert
        var entry = _context.AuditLogEntries.Single();
        entry.UserId.Should().Be("user-123");
        entry.UserDisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task LogAsync_SerializesOldAndNewValuesToJson()
    {
        // Arrange
        var oldValues = new { Name = "Old Name", Code = "OLD" };
        var newValues = new { Name = "New Name", Code = "NEW" };

        // Act
        await _sut.LogAsync("Update", "Company", "co-1", oldValues, newValues);

        // Assert
        var entry = _context.AuditLogEntries.Single();
        entry.OldValues.Should().Contain("Old Name");
        entry.NewValues.Should().Contain("New Name");
    }

    [Fact]
    public async Task LogAsync_SetsTimestampToUtcNow()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await _sut.LogAsync("Delete", "User", "user-id-1");

        // Assert
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        var entry = _context.AuditLogEntries.Single();
        entry.Timestamp.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public async Task LogAsync_WithNullOldAndNewValues_SetsNullJson()
    {
        // Act
        await _sut.LogAsync("Create", "Company", "co-1");

        // Assert
        var entry = _context.AuditLogEntries.Single();
        entry.OldValues.Should().BeNull();
        entry.NewValues.Should().BeNull();
    }
}
