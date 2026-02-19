namespace SupportHub.Application.DTOs;

public record TicketAttachmentDto(
    Guid Id,
    Guid TicketId,
    Guid? TicketMessageId,
    string FileName,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTimeOffset CreatedAt);
