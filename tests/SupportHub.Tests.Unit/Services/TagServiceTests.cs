namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;

public class TagServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TagService> _logger;
    private readonly TagService _sut;

    public TagServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<TagService>>();
        _sut = new TagService(_context, _currentUserService, _logger);

        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    public void Dispose() => _context.Dispose();

    private async Task<(Company company, Ticket ticket)> CreateTestTicketAsync(Company? company = null)
    {
        company ??= new Company { Name = "Test Co", Code = "TC" };
        if (!_context.Companies.Local.Contains(company))
        {
            _context.Companies.Add(company);
        }

        var ticket = new Ticket
        {
            CompanyId = company.Id,
            TicketNumber = $"TKT-{Guid.NewGuid():N}".Substring(0, 20),
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
    public async Task AddTagAsync_NewTag_ReturnsTagDto()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        // Act
        var result = await _sut.AddTagAsync(ticket.Id, "bug");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Tag.Should().Be("bug");
        result.Value.TicketId.Should().Be(ticket.Id);
    }

    [Fact]
    public async Task AddTagAsync_NormalizesToLowercase()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        // Act
        var result = await _sut.AddTagAsync(ticket.Id, "BUG");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Tag.Should().Be("bug");
    }

    [Fact]
    public async Task AddTagAsync_DuplicateTagSameTicket_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        await _sut.AddTagAsync(ticket.Id, "bug");

        // Act
        var result = await _sut.AddTagAsync(ticket.Id, "bug");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task AddTagAsync_DuplicateTagDifferentCase_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        await _sut.AddTagAsync(ticket.Id, "bug");

        // Act
        var result = await _sut.AddTagAsync(ticket.Id, "BUG");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task RemoveTagAsync_ExistingTag_SoftDeletes()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();
        await _sut.AddTagAsync(ticket.Id, "bug");

        // Act
        var result = await _sut.RemoveTagAsync(ticket.Id, "bug");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = await _context.TicketTags
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TicketId == ticket.Id && t.Tag == "bug");
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveTagAsync_NonExistentTag_ReturnsFailure()
    {
        // Arrange
        var (company, ticket) = await CreateTestTicketAsync();

        // Act
        var result = await _sut.RemoveTagAsync(ticket.Id, "nonexistent");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetPopularTagsAsync_ReturnsOrderedByFrequency()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        // Create 3 tickets each with "bug" tag
        for (int i = 0; i < 3; i++)
        {
            var (_, ticket) = await CreateTestTicketAsync(company);
            _context.TicketTags.Add(new TicketTag { TicketId = ticket.Id, Tag = "bug" });
        }

        // Create 1 ticket with "feature" tag
        var (_, featureTicket) = await CreateTestTicketAsync(company);
        _context.TicketTags.Add(new TicketTag { TicketId = featureTicket.Id, Tag = "feature" });

        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetPopularTagsAsync(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Value![0].Should().Be("bug");
    }

    [Fact]
    public async Task GetPopularTagsAsync_WithCompanyFilter_ReturnsOnlyMatchingCompany()
    {
        // Arrange
        var companyA = new Company { Name = "Company A", Code = "CA" };
        var companyB = new Company { Name = "Company B", Code = "CB" };
        _context.Companies.AddRange(companyA, companyB);
        await _context.SaveChangesAsync();

        var (_, ticketA) = await CreateTestTicketAsync(companyA);
        _context.TicketTags.Add(new TicketTag { TicketId = ticketA.Id, Tag = "alpha" });

        var (_, ticketB) = await CreateTestTicketAsync(companyB);
        _context.TicketTags.Add(new TicketTag { TicketId = ticketB.Id, Tag = "beta" });

        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetPopularTagsAsync(companyA.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().Contain("alpha");
        result.Value.Should().NotContain("beta");
    }
}
