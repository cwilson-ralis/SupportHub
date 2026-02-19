namespace SupportHub.Domain.Entities;

public class EmailProcessingLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmailConfigurationId { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? SenderEmail { get; set; }
    public string ProcessingResult { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;

    public EmailConfiguration EmailConfiguration { get; set; } = null!;
    public Ticket? Ticket { get; set; }
}
