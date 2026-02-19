namespace SupportHub.Application.DTOs;

public record EmailProcessingLogDto(
    Guid Id,
    Guid EmailConfigurationId,
    string ExternalMessageId,
    string? Subject,
    string? SenderEmail,
    string ProcessingResult,
    Guid? TicketId,
    string? ErrorMessage,
    DateTimeOffset ProcessedAt);
