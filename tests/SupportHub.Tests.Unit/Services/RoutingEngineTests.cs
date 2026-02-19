namespace SupportHub.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SupportHub.Application.DTOs;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;
using SupportHub.Tests.Unit.Helpers;

public class RoutingEngineTests : IDisposable
{
    private readonly SupportHubDbContext _context;
    private readonly ILogger<RoutingEngine> _logger;
    private readonly RoutingEngine _sut;

    public RoutingEngineTests()
    {
        _context = TestDbContextFactory.Create();
        _logger = Substitute.For<ILogger<RoutingEngine>>();
        _sut = new RoutingEngine(_context, _logger);
    }

    public void Dispose() => _context.Dispose();

    private async Task<(Company company, Queue queue, RoutingRule rule)> CreateScenarioAsync(
        RuleMatchType matchType,
        RuleMatchOperator matchOperator,
        string matchValue,
        bool isDefault = false,
        int sortOrder = 10,
        bool isActive = true,
        Guid? autoAssignAgentId = null,
        TicketPriority? autoSetPriority = null,
        string? autoAddTags = null)
    {
        var company = new Company { Name = "Test", Code = "T" };
        var queue = new Queue
        {
            CompanyId = company.Id,
            Name = "Q1",
            IsDefault = isDefault,
        };
        var rule = new RoutingRule
        {
            CompanyId = company.Id,
            QueueId = queue.Id,
            Queue = queue,
            Name = "Test Rule",
            MatchType = matchType,
            MatchOperator = matchOperator,
            MatchValue = matchValue,
            SortOrder = sortOrder,
            IsActive = isActive,
            AutoAssignAgentId = autoAssignAgentId,
            AutoSetPriority = autoSetPriority,
            AutoAddTags = autoAddTags,
        };
        _context.Companies.Add(company);
        _context.Queues.Add(queue);
        _context.RoutingRules.Add(rule);
        await _context.SaveChangesAsync();
        return (company, queue, rule);
    }

    [Fact]
    public async Task EvaluateAsync_SenderDomain_Equals_Matches()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "example.com");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "example.com",
            Subject: "Hello",
            Body: "Body text",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
        result.Value.MatchedRuleId.Should().Be(rule.Id);
        result.Value.IsDefaultFallback.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_SenderDomain_Equals_NoMatch()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "example.com");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "other.com",
            Subject: "Hello",
            Body: "Body text",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        // No match, no default queue — QueueId should be null
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().BeNull();
        result.Value.MatchedRuleId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_SubjectKeyword_Contains_Matches()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SubjectKeyword, RuleMatchOperator.Contains, "urgent");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "example.com",
            Subject: "This is an URGENT request",
            Body: "Body text",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
        result.Value.IsDefaultFallback.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_SubjectKeyword_Contains_NoMatch()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SubjectKeyword, RuleMatchOperator.Contains, "urgent");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "example.com",
            Subject: "Normal request",
            Body: "Body text",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().BeNull();
        result.Value.MatchedRuleId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_BodyKeyword_Regex_Matches()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.BodyKeyword, RuleMatchOperator.Regex, @"error\s+\d+");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: null,
            Subject: "Issue report",
            Body: "Got error 404 on page",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
        result.Value.MatchedRuleId.Should().Be(rule.Id);
    }

    [Fact]
    public async Task EvaluateAsync_BodyKeyword_Regex_InvalidRegex_NoMatch()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.BodyKeyword, RuleMatchOperator.Regex, "[invalid");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: null,
            Subject: "Hello",
            Body: "anything",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act — invalid regex should be handled gracefully
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().BeNull();
        result.Value.MatchedRuleId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_Tag_In_Matches()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.Tag, RuleMatchOperator.In, "billing,payments,refund");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: null,
            Subject: "Charge inquiry",
            Body: "I need help",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: ["shipping", "payments"]);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
        result.Value.MatchedRuleId.Should().Be(rule.Id);
    }

    [Fact]
    public async Task EvaluateAsync_Tag_In_NoMatch()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.Tag, RuleMatchOperator.In, "billing,payments,refund");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: null,
            Subject: "Return inquiry",
            Body: "I need help",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: ["shipping", "returns"]);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().BeNull();
        result.Value.MatchedRuleId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_FirstMatchWins()
    {
        // Arrange
        var company = new Company { Name = "Test", Code = "T" };
        var queue1 = new Queue { CompanyId = company.Id, Name = "VIP Queue" };
        var queue2 = new Queue { CompanyId = company.Id, Name = "Subject Queue" };

        var rule1 = new RoutingRule
        {
            CompanyId = company.Id,
            QueueId = queue1.Id,
            Queue = queue1,
            Name = "VIP Domain Rule",
            MatchType = RuleMatchType.SenderDomain,
            MatchOperator = RuleMatchOperator.Equals,
            MatchValue = "vip.com",
            SortOrder = 10,
            IsActive = true,
        };

        var rule2 = new RoutingRule
        {
            CompanyId = company.Id,
            QueueId = queue2.Id,
            Queue = queue2,
            Name = "VIP Subject Rule",
            MatchType = RuleMatchType.SubjectKeyword,
            MatchOperator = RuleMatchOperator.Contains,
            MatchValue = "vip",
            SortOrder = 20,
            IsActive = true,
        };

        _context.Companies.Add(company);
        _context.Queues.AddRange(queue1, queue2);
        _context.RoutingRules.AddRange(rule1, rule2);
        await _context.SaveChangesAsync();

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "vip.com",
            Subject: "vip customer",
            Body: "I need help",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert — first rule (SortOrder=10) should win
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue1.Id);
        result.Value.MatchedRuleId.Should().Be(rule1.Id);
    }

    [Fact]
    public async Task EvaluateAsync_InactiveRulesSkipped()
    {
        // Arrange
        var company = new Company { Name = "Test", Code = "T" };
        var queue1 = new Queue { CompanyId = company.Id, Name = "Queue 1" };
        var queue2 = new Queue { CompanyId = company.Id, Name = "Queue 2" };

        var inactiveRule = new RoutingRule
        {
            CompanyId = company.Id,
            QueueId = queue1.Id,
            Queue = queue1,
            Name = "Inactive Rule",
            MatchType = RuleMatchType.SenderDomain,
            MatchOperator = RuleMatchOperator.Equals,
            MatchValue = "example.com",
            SortOrder = 10,
            IsActive = false, // inactive — should be skipped
        };

        var activeRule = new RoutingRule
        {
            CompanyId = company.Id,
            QueueId = queue2.Id,
            Queue = queue2,
            Name = "Active Rule",
            MatchType = RuleMatchType.SubjectKeyword,
            MatchOperator = RuleMatchOperator.Contains,
            MatchValue = "help",
            SortOrder = 20,
            IsActive = true,
        };

        _context.Companies.Add(company);
        _context.Queues.AddRange(queue1, queue2);
        _context.RoutingRules.AddRange(inactiveRule, activeRule);
        await _context.SaveChangesAsync();

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "example.com",
            Subject: "I need help",
            Body: "Please help me",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert — inactive rule is skipped, active rule (queue2) should match
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue2.Id);
        result.Value.MatchedRuleId.Should().Be(activeRule.Id);
    }

    [Fact]
    public async Task EvaluateAsync_NoMatchWithDefaultQueue_ReturnsDefault()
    {
        // Arrange
        var company = new Company { Name = "Test", Code = "T" };
        var defaultQueue = new Queue
        {
            CompanyId = company.Id,
            Name = "Default Queue",
            IsDefault = true,
        };

        var rule = new RoutingRule
        {
            CompanyId = company.Id,
            QueueId = defaultQueue.Id,
            Queue = defaultQueue,
            Name = "Non-Matching Rule",
            MatchType = RuleMatchType.SenderDomain,
            MatchOperator = RuleMatchOperator.Equals,
            MatchValue = "specific.com",
            SortOrder = 10,
            IsActive = true,
        };

        _context.Companies.Add(company);
        _context.Queues.Add(defaultQueue);
        _context.RoutingRules.Add(rule);
        await _context.SaveChangesAsync();

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "nomatch.com",
            Subject: "Hello",
            Body: "Body",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert — no rule matched, fallback to default queue
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(defaultQueue.Id);
        result.Value.IsDefaultFallback.Should().BeTrue();
        result.Value.MatchedRuleId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_NoMatchNoDefaultQueue_ReturnsNullQueue()
    {
        // Arrange
        var company = new Company { Name = "Test", Code = "T" };
        var queue = new Queue
        {
            CompanyId = company.Id,
            Name = "Non-Default Queue",
            IsDefault = false,
        };

        var rule = new RoutingRule
        {
            CompanyId = company.Id,
            QueueId = queue.Id,
            Queue = queue,
            Name = "Non-Matching Rule",
            MatchType = RuleMatchType.SenderDomain,
            MatchOperator = RuleMatchOperator.Equals,
            MatchValue = "specific.com",
            SortOrder = 10,
            IsActive = true,
        };

        _context.Companies.Add(company);
        _context.Queues.Add(queue);
        _context.RoutingRules.Add(rule);
        await _context.SaveChangesAsync();

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "nomatch.com",
            Subject: "Hello",
            Body: "Body",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert — no match and no default queue
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().BeNull();
        result.Value.IsDefaultFallback.Should().BeFalse();
        result.Value.MatchedRuleId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_AutoAssignAndPriority_AppliedOnMatch()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "priority.com",
            autoAssignAgentId: agentId,
            autoSetPriority: TicketPriority.High);

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "priority.com",
            Subject: "Hello",
            Body: "Body",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
        result.Value.AutoAssignAgentId.Should().Be(agentId);
        result.Value.AutoSetPriority.Should().Be(TicketPriority.High);
    }

    [Fact]
    public async Task EvaluateAsync_AutoAddTags_ParsedFromCommaSeparated()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "tagged.com",
            autoAddTags: "billing, urgent , vip");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "tagged.com",
            Subject: "Hello",
            Body: "Body",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
        result.Value.AutoAddTags.Should().HaveCount(3);
        result.Value.AutoAddTags.Should().Contain("billing");
        result.Value.AutoAddTags.Should().Contain("urgent");
        result.Value.AutoAddTags.Should().Contain("vip");
    }

    [Fact]
    public async Task EvaluateAsync_SenderDomain_CaseInsensitive_Matches()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "Example.COM");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: "example.com",
            Subject: "Hello",
            Body: "Body",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert — matching should be case-insensitive
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
    }

    [Fact]
    public async Task EvaluateAsync_DifferentCompany_DoesNotMatch()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.SenderDomain, RuleMatchOperator.Equals, "example.com");

        var otherCompanyId = Guid.NewGuid();

        var context = new RoutingContext(
            CompanyId: otherCompanyId, // different company
            SenderDomain: "example.com",
            Subject: "Hello",
            Body: "Body",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert — rule belongs to different company, should not match
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_BodyKeyword_StartsWith_Matches()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.BodyKeyword, RuleMatchOperator.StartsWith, "Error:");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: null,
            Subject: "System Failure",
            Body: "Error: Something went wrong",
            IssueType: null,
            System: null,
            RequesterEmail: null,
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
    }

    [Fact]
    public async Task EvaluateAsync_RequesterEmail_Equals_Matches()
    {
        // Arrange
        var (company, queue, rule) = await CreateScenarioAsync(
            RuleMatchType.RequesterEmail, RuleMatchOperator.Equals, "vip@client.com");

        var context = new RoutingContext(
            CompanyId: company.Id,
            SenderDomain: null,
            Subject: "Support request",
            Body: "I need help",
            IssueType: null,
            System: null,
            RequesterEmail: "vip@client.com",
            Tags: []);

        // Act
        var result = await _sut.EvaluateAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueueId.Should().Be(queue.Id);
        result.Value.MatchedRuleId.Should().Be(rule.Id);
    }
}
