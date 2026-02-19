namespace SupportHub.Application.DTOs;

public record QueueDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive,
    int TicketCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreateQueueRequest(
    Guid CompanyId,
    string Name,
    string? Description,
    bool IsDefault
);

public record UpdateQueueRequest(
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive
);
