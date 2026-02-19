namespace SupportHub.Domain.Entities;

public class TicketTag : BaseEntity
{
    public Guid TicketId { get; set; }
    public string Tag { get; set; } = string.Empty;

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
}
