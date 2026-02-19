namespace SupportHub.Infrastructure.Services;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class RoutingEngine(
    SupportHubDbContext _context,
    ILogger<RoutingEngine> _logger) : IRoutingEngine
{
    public async Task<Result<RoutingResult>> EvaluateAsync(RoutingContext context, CancellationToken ct = default)
    {
        var rules = await _context.RoutingRules
            .AsNoTracking()
            .Include(r => r.Queue)
            .Where(r => r.CompanyId == context.CompanyId && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        foreach (var rule in rules)
        {
            if (EvaluateRule(rule, context))
            {
                _logger.LogInformation(
                    "Routing rule {RuleId} ({RuleName}) matched for company {CompanyId}",
                    rule.Id, rule.Name, context.CompanyId);

                var autoAddTags = ParseTags(rule.AutoAddTags);

                return Result<RoutingResult>.Success(new RoutingResult(
                    QueueId: rule.QueueId,
                    QueueName: rule.Queue?.Name,
                    AutoAssignAgentId: rule.AutoAssignAgentId,
                    AutoSetPriority: rule.AutoSetPriority,
                    AutoAddTags: autoAddTags,
                    MatchedRuleId: rule.Id,
                    MatchedRuleName: rule.Name,
                    IsDefaultFallback: false));
            }
        }

        // No rule matched — find default queue
        _logger.LogInformation(
            "No routing rules matched for company {CompanyId}, falling back to default queue",
            context.CompanyId);

        var defaultQueue = await _context.Queues
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.CompanyId == context.CompanyId && q.IsDefault && !q.IsDeleted, ct);

        if (defaultQueue is not null)
        {
            return Result<RoutingResult>.Success(new RoutingResult(
                QueueId: defaultQueue.Id,
                QueueName: defaultQueue.Name,
                AutoAssignAgentId: null,
                AutoSetPriority: null,
                AutoAddTags: [],
                MatchedRuleId: null,
                MatchedRuleName: null,
                IsDefaultFallback: true));
        }

        // No default queue either
        return Result<RoutingResult>.Success(new RoutingResult(
            QueueId: null,
            QueueName: null,
            AutoAssignAgentId: null,
            AutoSetPriority: null,
            AutoAddTags: [],
            MatchedRuleId: null,
            MatchedRuleName: null,
            IsDefaultFallback: false));
    }

    private static bool EvaluateRule(RoutingRule rule, RoutingContext context)
    {
        switch (rule.MatchType)
        {
            case RuleMatchType.SenderDomain:
                return ApplyOperator(context.SenderDomain ?? string.Empty, rule.MatchValue, rule.MatchOperator);

            case RuleMatchType.SubjectKeyword:
                return ApplyOperator(context.Subject, rule.MatchValue, rule.MatchOperator);

            case RuleMatchType.BodyKeyword:
                return ApplyOperator(context.Body, rule.MatchValue, rule.MatchOperator);

            case RuleMatchType.IssueType:
                return ApplyOperator(context.IssueType ?? string.Empty, rule.MatchValue, rule.MatchOperator);

            case RuleMatchType.System:
                return ApplyOperator(context.System ?? string.Empty, rule.MatchValue, rule.MatchOperator);

            case RuleMatchType.RequesterEmail:
                return ApplyOperator(context.RequesterEmail ?? string.Empty, rule.MatchValue, rule.MatchOperator);

            case RuleMatchType.CompanyCode:
                // Company code lookup not available in context
                return false;

            case RuleMatchType.Tag:
                return EvaluateTagRule(context.Tags, rule.MatchValue, rule.MatchOperator);

            default:
                return false;
        }
    }

    private static bool EvaluateTagRule(IReadOnlyList<string> tags, string matchValue, RuleMatchOperator op)
    {
        if (tags.Count == 0)
            return false;

        if (op == RuleMatchOperator.In)
        {
            var allowedValues = matchValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return tags.Any(tag => allowedValues.Any(v => string.Equals(tag, v, StringComparison.OrdinalIgnoreCase)));
        }

        if (op == RuleMatchOperator.Contains)
            return tags.Any(tag => tag.Contains(matchValue, StringComparison.OrdinalIgnoreCase));

        if (op == RuleMatchOperator.Equals)
            return tags.Any(tag => string.Equals(tag, matchValue, StringComparison.OrdinalIgnoreCase));

        // For other operators, apply to each tag and return true if any match
        return tags.Any(tag => ApplyOperator(tag, matchValue, op));
    }

    private static bool ApplyOperator(string value, string matchValue, RuleMatchOperator op)
    {
        return op switch
        {
            RuleMatchOperator.Equals =>
                string.Equals(value, matchValue, StringComparison.OrdinalIgnoreCase),

            RuleMatchOperator.Contains =>
                value.Contains(matchValue, StringComparison.OrdinalIgnoreCase),

            RuleMatchOperator.StartsWith =>
                value.StartsWith(matchValue, StringComparison.OrdinalIgnoreCase),

            RuleMatchOperator.EndsWith =>
                value.EndsWith(matchValue, StringComparison.OrdinalIgnoreCase),

            RuleMatchOperator.Regex =>
                EvaluateRegex(value, matchValue),

            RuleMatchOperator.In =>
                matchValue
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(v => string.Equals(value, v, StringComparison.OrdinalIgnoreCase)),

            _ => false,
        };
    }

    private static bool EvaluateRegex(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ParseTags(string? autoAddTags)
    {
        if (string.IsNullOrWhiteSpace(autoAddTags))
            return [];

        return autoAddTags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
    }
}
