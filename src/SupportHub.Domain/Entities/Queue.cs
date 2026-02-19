namespace SupportHub.Domain.Entities;

public class Queue : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; } = null;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Company Company { get; set; } = null!;
    public ICollection<RoutingRule> RoutingRules { get; set; } = new List<RoutingRule>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
