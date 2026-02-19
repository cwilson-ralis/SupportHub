namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;

public class CannedResponseServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CannedResponseService> _logger;
    private readonly CannedResponseService _sut;

    public CannedResponseServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<CannedResponseService>>();
        _sut = new CannedResponseService(_context, _currentUserService, _logger);

        _currentUserService.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetCannedResponsesAsync_WithCompanyId_ReturnsCompanyAndGlobal()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);

        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = company.Id, Title = "Company Response 1", Body = "Body 1", IsActive = true, SortOrder = 1
        });
        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = company.Id, Title = "Company Response 2", Body = "Body 2", IsActive = true, SortOrder = 2
        });
        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = null, Title = "Global Response", Body = "Body G", IsActive = true, SortOrder = 1
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetCannedResponsesAsync(company.Id, 1, 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetCannedResponsesAsync_WithoutCompanyId_ReturnsGlobalOnly()
    {
        // Arrange
        var company = new Company { Name = "Test Co", Code = "TC" };
        _context.Companies.Add(company);

        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = company.Id, Title = "Company Response", Body = "Body C", IsActive = true, SortOrder = 1
        });
        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = null, Title = "Global Response", Body = "Body G", IsActive = true, SortOrder = 1
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetCannedResponsesAsync(null, 1, 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.First().Title.Should().Be("Global Response");
    }

    [Fact]
    public async Task GetCannedResponsesAsync_OrdersBySortOrderThenTitle()
    {
        // Arrange
        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = null, Title = "Zebra", Body = "Body Z", IsActive = true, SortOrder = 3
        });
        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = null, Title = "Alpha", Body = "Body A", IsActive = true, SortOrder = 1
        });
        _context.CannedResponses.Add(new CannedResponse
        {
            CompanyId = null, Title = "Bravo", Body = "Body B", IsActive = true, SortOrder = 2
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetCannedResponsesAsync(null, 1, 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items[0].Title.Should().Be("Alpha");
        result.Value.Items[1].Title.Should().Be("Bravo");
        result.Value.Items[2].Title.Should().Be("Zebra");
    }

    [Fact]
    public async Task CreateCannedResponseAsync_ValidRequest_ReturnsDto()
    {
        // Arrange
        var request = new CreateCannedResponseRequest(null, "Greeting", "Hello, how can I help?", "General", 1);

        // Act
        var result = await _sut.CreateCannedResponseAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Greeting");
        result.Value.Body.Should().Be("Hello, how can I help?");
    }

    [Fact]
    public async Task UpdateCannedResponseAsync_ValidRequest_UpdatesTitle()
    {
        // Arrange
        var entity = new CannedResponse
        {
            CompanyId = null, Title = "Old Title", Body = "Body", IsActive = true, SortOrder = 1
        };
        _context.CannedResponses.Add(entity);
        await _context.SaveChangesAsync();

        var request = new UpdateCannedResponseRequest("New Title", null, null, null, null);

        // Act
        var result = await _sut.UpdateCannedResponseAsync(entity.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("New Title");
    }

    [Fact]
    public async Task DeleteCannedResponseAsync_ExistingResponse_SoftDeletes()
    {
        // Arrange
        var entity = new CannedResponse
        {
            CompanyId = null, Title = "To Delete", Body = "Body", IsActive = true, SortOrder = 1
        };
        _context.CannedResponses.Add(entity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteCannedResponseAsync(entity.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = await _context.CannedResponses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == entity.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }
}
