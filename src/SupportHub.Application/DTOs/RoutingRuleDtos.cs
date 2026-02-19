namespace SupportHub.Application.DTOs;

using SupportHub.Domain.Enums;

public record RoutingRuleDto(
    Guid Id,
    Guid CompanyId,
    Guid QueueId,
    string QueueName,
    string Name,
    string? Description,
    string MatchType,       // stored as string for serialization
    string MatchOperator,   // stored as string for serialization
    string MatchValue,
    int SortOrder,
    bool IsActive,
    Guid? AutoAssignAgentId,
    string? AutoAssignAgentName,
    string? AutoSetPriority, // stored as string for serialization (nullable)
    string? AutoAddTags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreateRoutingRuleRequest(
    Guid CompanyId,
    Guid QueueId,
    string Name,
    string? Description,
    RuleMatchType MatchType,
    RuleMatchOperator MatchOperator,
    string MatchValue,
    Guid? AutoAssignAgentId,
    TicketPriority? AutoSetPriority,
    string? AutoAddTags
);

public record UpdateRoutingRuleRequest(
    Guid QueueId,
    string Name,
    string? Description,
    RuleMatchType MatchType,
    RuleMatchOperator MatchOperator,
    string MatchValue,
    bool IsActive,
    Guid? AutoAssignAgentId,
    TicketPriority? AutoSetPriority,
    string? AutoAddTags
);

public record ReorderRoutingRulesRequest(
    IReadOnlyList<Guid> RuleIdsInOrder
);

// Used by the routing engine to evaluate rules
public record RoutingContext(
    Guid CompanyId,
    string? SenderDomain,
    string Subject,
    string Body,
    string? IssueType,
    string? System,
    string? RequesterEmail,
    IReadOnlyList<string> Tags
);

// Result returned by the routing engine
public record RoutingResult(
    Guid? QueueId,
    string? QueueName,
    Guid? AutoAssignAgentId,
    TicketPriority? AutoSetPriority,
    IReadOnlyList<string> AutoAddTags,
    Guid? MatchedRuleId,
    string? MatchedRuleName,
    bool IsDefaultFallback
);
