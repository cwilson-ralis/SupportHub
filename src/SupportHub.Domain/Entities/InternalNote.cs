namespace SupportHub.Domain.Entities;

public class InternalNote : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = string.Empty;

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
}
