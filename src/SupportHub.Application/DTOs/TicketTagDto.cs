namespace SupportHub.Application.DTOs;

public record TicketTagDto(
    Guid Id,
    Guid TicketId,
    string Tag);
