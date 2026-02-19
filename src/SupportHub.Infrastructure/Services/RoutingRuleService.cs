namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

public class RoutingRuleService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUser,
    IAuditService _audit,
    ILogger<RoutingRuleService> _logger) : IRoutingRuleService
{
    public async Task<Result<IReadOnlyList<RoutingRuleDto>>> GetRulesAsync(Guid companyId, CancellationToken ct = default)
    {
        if (!await _currentUser.HasAccessToCompanyAsync(companyId, ct))
            return Result<IReadOnlyList<RoutingRuleDto>>.Failure("Access denied.");

        var rules = await _context.RoutingRules
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.SortOrder)
            .Select(r => new RoutingRuleDto(
                r.Id,
                r.CompanyId,
                r.QueueId,
                r.Queue.Name,
                r.Name,
                r.Description,
                r.MatchType.ToString(),
                r.MatchOperator.ToString(),
                r.MatchValue,
                r.SortOrder,
                r.IsActive,
                r.AutoAssignAgentId,
                r.AutoAssignAgent != null ? r.AutoAssignAgent.DisplayName : null,
                r.AutoSetPriority != null ? r.AutoSetPriority.ToString() : null,
                r.AutoAddTags,
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RoutingRuleDto>>.Success(rules);
    }

    public async Task<Result<RoutingRuleDto>> GetRuleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _context.RoutingRules
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.CompanyId,
                r.QueueId,
                QueueName = r.Queue.Name,
                r.Name,
                r.Description,
                r.MatchType,
                r.MatchOperator,
                r.MatchValue,
                r.SortOrder,
                r.IsActive,
                r.AutoAssignAgentId,
                AutoAssignAgentName = r.AutoAssignAgent != null ? r.AutoAssignAgent.DisplayName : null,
                r.AutoSetPriority,
                r.AutoAddTags,
                r.CreatedAt,
                r.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (rule is null)
            return Result<RoutingRuleDto>.Failure("Routing rule not found.");

        if (!await _currentUser.HasAccessToCompanyAsync(rule.CompanyId, ct))
            return Result<RoutingRuleDto>.Failure("Access denied.");

        return Result<RoutingRuleDto>.Success(new RoutingRuleDto(
            rule.Id,
            rule.CompanyId,
            rule.QueueId,
            rule.QueueName,
            rule.Name,
            rule.Description,
            rule.MatchType.ToString(),
            rule.MatchOperator.ToString(),
            rule.MatchValue,
            rule.SortOrder,
            rule.IsActive,
            rule.AutoAssignAgentId,
            rule.AutoAssignAgentName,
            rule.AutoSetPriority?.ToString(),
            rule.AutoAddTags,
            rule.CreatedAt,
            rule.UpdatedAt));
    }

    public async Task<Result<RoutingRuleDto>> CreateRuleAsync(CreateRoutingRuleRequest request, CancellationToken ct = default)
    {
        if (!await _currentUser.HasAccessToCompanyAsync(request.CompanyId, ct))
            return Result<RoutingRuleDto>.Failure("Access denied.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<RoutingRuleDto>.Failure("Routing rule name is required.");

        // Validate QueueId belongs to the same company
        var queue = await _context.Queues
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.QueueId, ct);
        if (queue is null)
            return Result<RoutingRuleDto>.Failure("Queue not found.");
        if (queue.CompanyId != request.CompanyId)
            return Result<RoutingRuleDto>.Failure("Queue does not belong to this company.");

        // Auto-assign SortOrder
        var maxSortOrder = await _context.RoutingRules
            .Where(r => r.CompanyId == request.CompanyId)
            .Select(r => (int?)r.SortOrder)
            .MaxAsync(ct);
        var sortOrder = (maxSortOrder ?? 0) + 10;

        var rule = new RoutingRule
        {
            CompanyId = request.CompanyId,
            QueueId = request.QueueId,
            Name = request.Name,
            Description = request.Description,
            MatchType = request.MatchType,
            MatchOperator = request.MatchOperator,
            MatchValue = request.MatchValue,
            SortOrder = sortOrder,
            IsActive = true,
            AutoAssignAgentId = request.AutoAssignAgentId,
            AutoSetPriority = request.AutoSetPriority,
            AutoAddTags = request.AutoAddTags,
        };

        _context.RoutingRules.Add(rule);
        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("Created", "RoutingRule", rule.Id.ToString(),
            newValues: new { rule.Name, rule.CompanyId, rule.QueueId, rule.MatchType, rule.SortOrder }, ct: ct);

        _logger.LogInformation("Created routing rule {RuleName} for company {CompanyId}", rule.Name, rule.CompanyId);

        return await GetRuleByIdAsync(rule.Id, ct);
    }

    public async Task<Result<RoutingRuleDto>> UpdateRuleAsync(Guid id, UpdateRoutingRuleRequest request, CancellationToken ct = default)
    {
        var rule = await _context.RoutingRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return Result<RoutingRuleDto>.Failure("Routing rule not found.");

        if (!await _currentUser.HasAccessToCompanyAsync(rule.CompanyId, ct))
            return Result<RoutingRuleDto>.Failure("Access denied.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<RoutingRuleDto>.Failure("Routing rule name is required.");

        // Validate QueueId belongs to the same company
        var queue = await _context.Queues
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.QueueId, ct);
        if (queue is null)
            return Result<RoutingRuleDto>.Failure("Queue not found.");
        if (queue.CompanyId != rule.CompanyId)
            return Result<RoutingRuleDto>.Failure("Queue does not belong to this company.");

        var oldValues = new { rule.Name, rule.QueueId, rule.MatchType, rule.MatchOperator, rule.MatchValue, rule.IsActive };

        rule.QueueId = request.QueueId;
        rule.Name = request.Name;
        rule.Description = request.Description;
        rule.MatchType = request.MatchType;
        rule.MatchOperator = request.MatchOperator;
        rule.MatchValue = request.MatchValue;
        rule.IsActive = request.IsActive;
        rule.AutoAssignAgentId = request.AutoAssignAgentId;
        rule.AutoSetPriority = request.AutoSetPriority;
        rule.AutoAddTags = request.AutoAddTags;

        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("Updated", "RoutingRule", rule.Id.ToString(),
            oldValues: oldValues,
            newValues: new { rule.Name, rule.QueueId, rule.MatchType, rule.MatchOperator, rule.MatchValue, rule.IsActive },
            ct: ct);

        _logger.LogInformation("Updated routing rule {RuleId} ({RuleName})", rule.Id, rule.Name);

        return await GetRuleByIdAsync(rule.Id, ct);
    }

    public async Task<Result<bool>> DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _context.RoutingRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return Result<bool>.Failure("Routing rule not found.");

        if (!await _currentUser.HasAccessToCompanyAsync(rule.CompanyId, ct))
            return Result<bool>.Failure("Access denied.");

        rule.IsDeleted = true;
        rule.DeletedAt = DateTimeOffset.UtcNow;
        rule.DeletedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("Deleted", "RoutingRule", rule.Id.ToString(), ct: ct);

        _logger.LogInformation("Deleted routing rule {RuleId} ({RuleName})", rule.Id, rule.Name);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReorderRulesAsync(Guid companyId, ReorderRoutingRulesRequest request, CancellationToken ct = default)
    {
        if (!await _currentUser.HasAccessToCompanyAsync(companyId, ct))
            return Result<bool>.Failure("Access denied.");

        var ruleIds = request.RuleIdsInOrder.ToList();

        var rules = await _context.RoutingRules
            .Where(r => r.CompanyId == companyId && ruleIds.Contains(r.Id))
            .ToListAsync(ct);

        // Validate all IDs belong to this company
        if (rules.Count != ruleIds.Count)
            return Result<bool>.Failure("One or more rule IDs are invalid or do not belong to this company.");

        // Build lookup for fast access
        var ruleById = rules.ToDictionary(r => r.Id);

        // Re-assign SortOrder sequentially: 10, 20, 30...
        for (var i = 0; i < ruleIds.Count; i++)
        {
            ruleById[ruleIds[i]].SortOrder = (i + 1) * 10;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Reordered {Count} routing rules for company {CompanyId}", ruleIds.Count, companyId);

        return Result<bool>.Success(true);
    }
}
