using SupportHub.Domain.Enums;

namespace SupportHub.Domain.Entities;

public class TicketMessage : BaseEntity
{
    public Guid TicketId { get; set; }
    public MessageDirection Direction { get; set; }
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? ExternalMessageId { get; set; }

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}
