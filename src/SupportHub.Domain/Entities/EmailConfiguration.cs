using SupportHub.Domain.Enums;

namespace SupportHub.Domain.Entities;

public class EmailConfiguration : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string SharedMailboxAddress { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int PollingIntervalMinutes { get; set; } = 2;
    public DateTimeOffset? LastPolledAt { get; set; }
    public string? LastPolledMessageId { get; set; }
    public bool AutoCreateTickets { get; set; } = true;
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;

    public Company Company { get; set; } = null!;
}
