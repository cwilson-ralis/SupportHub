using SupportHub.Domain.Enums;

namespace SupportHub.Domain.Entities;

public class Ticket : BaseEntity
{
    // Identity
    public Guid CompanyId { get; set; }
    public Guid? QueueId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;

    // Core fields
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketSource Source { get; set; }

    // Requester
    public string RequesterEmail { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;

    // Assignment
    public Guid? AssignedAgentId { get; set; }

    // Categorization
    public string? System { get; set; }
    public string? IssueType { get; set; }

    // Lifecycle timestamps
    public DateTimeOffset? FirstResponseAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    // AI
    public string? AiClassification { get; set; }

    // Navigation properties
    public Company Company { get; set; } = null!;
    public Queue? Queue { get; set; } = null;
    public ApplicationUser? AssignedAgent { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<InternalNote> InternalNotes { get; set; } = new List<InternalNote>();
    public ICollection<TicketTag> Tags { get; set; } = new List<TicketTag>();
}
