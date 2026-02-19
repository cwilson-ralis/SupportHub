namespace SupportHub.Domain.Entities;

public class TicketAttachment : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid? TicketMessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
    public TicketMessage? TicketMessage { get; set; }
}
