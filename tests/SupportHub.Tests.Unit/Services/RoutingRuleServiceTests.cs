namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class RoutingRuleServiceTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<RoutingRuleService> _logger;
    private readonly RoutingRuleService _sut;

    public RoutingRuleServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _currentUser = Substitute.For<ICurrentUserService>();
        _audit = Substitute.For<IAuditService>();
        _logger = Substitute.For<ILogger<RoutingRuleService>>();
        _sut = new RoutingRuleService(_context, _currentUser, _audit, _logger);

        _currentUser.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _currentUser.UserId.Returns("test-user-id");
    }

    public void Dispose() => _context.Dispose();

    private async Task<Company> CreateCompanyAsync(string name = "Test Co", string code = "TC")
    {
        var company = new Company { Name = name, Code = code };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        return company;
    }

    private async Task<Queue> CreateQueueAsync(Guid companyId, string name = "Support Queue")
    {
        var queue = new Queue
        {
            CompanyId = companyId,
            Name = name,
            IsDefault = false,
            IsActive = true,
        };
        _context.Queues.Add(queue);
        await _context.SaveChangesAsync();
        return queue;
    }

    private async Task<RoutingRule> CreateRuleAsync(Guid companyId, Guid queueId, string name = "Test Rule",
        int sortOrder = 10, bool isActive = true,
        RuleMatchType matchType = RuleMatchType.SenderDomain,
        RuleMatchOperator matchOperator = RuleMatchOperator.Equals,
        string matchValue = "example.com")
    {
        var rule = new RoutingRule
        {
            CompanyId = companyId,
            QueueId = queueId,
            Name = name,
            SortOrder = sortOrder,
            IsActive = isActive,
            MatchType = matchType,
            MatchOperator = matchOperator,
            MatchValue = matchValue,
        };
        _context.RoutingRules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    [Fact]
    public async Task GetRulesAsync_ValidCompany_ReturnsRulesOrderedBySortOrder()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);

        await CreateRuleAsync(company.Id, queue.Id, "Rule C", sortOrder: 30);
        await CreateRuleAsync(company.Id, queue.Id, "Rule A", sortOrder: 10);
        await CreateRuleAsync(company.Id, queue.Id, "Rule B", sortOrder: 20);

        // Act
        var result = await _sut.GetRulesAsync(company.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
        result.Value[0].Name.Should().Be("Rule A");
        result.Value[1].Name.Should().Be("Rule B");
        result.Value[2].Name.Should().Be("Rule C");
    }

    [Fact]
    public async Task GetRulesAsync_NoAccess_ReturnsFailure()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        _currentUser.HasAccessToCompanyAsync(company.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _sut.GetRulesAsync(company.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task CreateRuleAsync_ValidRequest_CreatesWithAutoSortOrder()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);

        var request1 = new CreateRoutingRuleRequest(
            company.Id, queue.Id, "First Rule", null,
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "example.com",
            null, null, null);

        // Act — create first rule
        var result1 = await _sut.CreateRuleAsync(request1);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result1.Value!.SortOrder.Should().Be(10);

        var request2 = new CreateRoutingRuleRequest(
            company.Id, queue.Id, "Second Rule", null,
            RuleMatchType.SubjectKeyword, RuleMatchOperator.Contains, "urgent",
            null, null, null);

        // Act — create second rule
        var result2 = await _sut.CreateRuleAsync(request2);

        // Assert
        result2.IsSuccess.Should().BeTrue();
        result2.Value!.SortOrder.Should().Be(20);
    }

    [Fact]
    public async Task CreateRuleAsync_QueueNotInSameCompany_ReturnsFailure()
    {
        // Arrange
        var companyA = await CreateCompanyAsync("Company A", "CA");
        var companyB = await CreateCompanyAsync("Company B", "CB");
        var queueA = await CreateQueueAsync(companyA.Id, "Company A Queue");

        // Try to create a rule for Company B using Company A's queue
        var request = new CreateRoutingRuleRequest(
            companyB.Id, queueA.Id, "Cross-Company Rule", null,
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "example.com",
            null, null, null);

        // Act
        var result = await _sut.CreateRuleAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Queue does not belong to this company");
    }

    [Fact]
    public async Task CreateRuleAsync_ValidRequest_AuditLogged()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);

        var request = new CreateRoutingRuleRequest(
            company.Id, queue.Id, "Audit Test Rule", null,
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "example.com",
            null, null, null);

        // Act
        var result = await _sut.CreateRuleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _audit.Received(1).LogAsync("Created", "RoutingRule", Arg.Any<string>(),
            oldValues: Arg.Any<object?>(), newValues: Arg.Any<object?>(), ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRuleAsync_ValidRequest_UpdatesRule()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);
        var rule = await CreateRuleAsync(company.Id, queue.Id, "Original Name", matchValue: "old.com");

        var request = new UpdateRoutingRuleRequest(
            queue.Id, "Updated Name", "Updated description",
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "new.com",
            true, null, null, null);

        // Act
        var result = await _sut.UpdateRuleAsync(rule.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Updated Name");
        result.Value.MatchValue.Should().Be("new.com");

        var inDb = await _context.RoutingRules.FindAsync(rule.Id);
        inDb!.Name.Should().Be("Updated Name");
        inDb.MatchValue.Should().Be("new.com");
    }

    [Fact]
    public async Task DeleteRuleAsync_SoftDeletes()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);
        var rule = await CreateRuleAsync(company.Id, queue.Id, "Rule To Delete");

        // Act
        var result = await _sut.DeleteRuleAsync(rule.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var deleted = await _context.RoutingRules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == rule.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
        deleted.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReorderRulesAsync_ValidRequest_ReassignsSortOrders()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);

        var rule1 = await CreateRuleAsync(company.Id, queue.Id, "Rule 1", sortOrder: 10);
        var rule2 = await CreateRuleAsync(company.Id, queue.Id, "Rule 2", sortOrder: 20);
        var rule3 = await CreateRuleAsync(company.Id, queue.Id, "Rule 3", sortOrder: 30);

        // Reorder: rule3 first, rule2 second, rule1 last
        var request = new ReorderRoutingRulesRequest(
            new List<Guid> { rule3.Id, rule2.Id, rule1.Id });

        // Act
        var result = await _sut.ReorderRulesAsync(company.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _context.ChangeTracker.Clear();
        var updatedRule3 = await _context.RoutingRules.FindAsync(rule3.Id);
        var updatedRule2 = await _context.RoutingRules.FindAsync(rule2.Id);
        var updatedRule1 = await _context.RoutingRules.FindAsync(rule1.Id);

        updatedRule3!.SortOrder.Should().Be(10);
        updatedRule2!.SortOrder.Should().Be(20);
        updatedRule1!.SortOrder.Should().Be(30);
    }

    [Fact]
    public async Task ReorderRulesAsync_InvalidRuleId_ReturnsFailure()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);

        var rule1 = await CreateRuleAsync(company.Id, queue.Id, "Rule 1", sortOrder: 10);
        var bogusId = Guid.NewGuid();

        var request = new ReorderRoutingRulesRequest(
            new List<Guid> { rule1.Id, bogusId });

        // Act
        var result = await _sut.ReorderRulesAsync(company.Id, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("invalid");
    }

    [Fact]
    public async Task GetRulesAsync_CompanyIsolation_OnlyReturnsOwnRules()
    {
        // Arrange
        var companyA = await CreateCompanyAsync("Company A", "CA");
        var companyB = await CreateCompanyAsync("Company B", "CB");

        var queueA = await CreateQueueAsync(companyA.Id, "Queue A");
        var queueB = await CreateQueueAsync(companyB.Id, "Queue B");

        await CreateRuleAsync(companyA.Id, queueA.Id, "A Rule 1", sortOrder: 10);
        await CreateRuleAsync(companyA.Id, queueA.Id, "A Rule 2", sortOrder: 20);
        await CreateRuleAsync(companyB.Id, queueB.Id, "B Rule 1", sortOrder: 10);
        await CreateRuleAsync(companyB.Id, queueB.Id, "B Rule 2", sortOrder: 20);
        await CreateRuleAsync(companyB.Id, queueB.Id, "B Rule 3", sortOrder: 30);

        // User has access to Company A only
        _currentUser.HasAccessToCompanyAsync(companyA.Id, Arg.Any<CancellationToken>()).Returns(true);
        _currentUser.HasAccessToCompanyAsync(companyB.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var resultA = await _sut.GetRulesAsync(companyA.Id);
        var resultB = await _sut.GetRulesAsync(companyB.Id);

        // Assert
        resultA.IsSuccess.Should().BeTrue();
        resultA.Value!.Should().HaveCount(2);

        resultB.IsSuccess.Should().BeFalse();
        resultB.Error.Should().Contain("Access denied");
    }

    [Fact]
    public async Task GetRuleByIdAsync_ExistingRule_ReturnsDto()
    {
        // Arrange
        var company = await CreateCompanyAsync();
        var queue = await CreateQueueAsync(company.Id);
        var rule = await CreateRuleAsync(company.Id, queue.Id, "My Rule", matchValue: "test.com");

        // Act
        var result = await _sut.GetRuleByIdAsync(rule.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(rule.Id);
        result.Value.Name.Should().Be("My Rule");
        result.Value.MatchValue.Should().Be("test.com");
    }

    [Fact]
    public async Task GetRuleByIdAsync_NotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetRuleByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task DeleteRuleAsync_NotFound_ReturnsFailure()
    {
        // Act
        var result = await _sut.DeleteRuleAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
}
