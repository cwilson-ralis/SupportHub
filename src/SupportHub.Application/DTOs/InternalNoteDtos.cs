namespace SupportHub.Application.DTOs;

public record InternalNoteDto(
    Guid Id,
    Guid TicketId,
    Guid AuthorId,
    string AuthorName,
    string Body,
    DateTimeOffset CreatedAt);

public record CreateInternalNoteRequest(
    string Body);
