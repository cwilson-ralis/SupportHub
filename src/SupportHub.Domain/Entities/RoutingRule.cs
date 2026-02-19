using SupportHub.Domain.Enums;

namespace SupportHub.Domain.Entities;

public class RoutingRule : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid QueueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; } = null;
    public RuleMatchType MatchType { get; set; }
    public RuleMatchOperator MatchOperator { get; set; }
    public string MatchValue { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? AutoAssignAgentId { get; set; } = null;
    public TicketPriority? AutoSetPriority { get; set; } = null;
    public string? AutoAddTags { get; set; } = null;

    // Navigation properties
    public Company Company { get; set; } = null!;
    public Queue Queue { get; set; } = null!;
    public ApplicationUser? AutoAssignAgent { get; set; } = null;
}
