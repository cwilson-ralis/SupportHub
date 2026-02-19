namespace SupportHub.Application.DTOs;

using SupportHub.Domain.Enums;

public record TicketMessageDto(
    Guid Id,
    Guid TicketId,
    MessageDirection Direction,
    string? SenderEmail,
    string? SenderName,
    string Body,
    string? HtmlBody,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    DateTimeOffset CreatedAt);

public record CreateTicketMessageRequest(
    MessageDirection Direction,
    string? SenderEmail,
    string? SenderName,
    string Body,
    string? HtmlBody);
