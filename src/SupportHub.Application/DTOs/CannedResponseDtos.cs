namespace SupportHub.Application.DTOs;

public record CannedResponseDto(
    Guid Id,
    Guid? CompanyId,
    string? CompanyName,
    string Title,
    string Body,
    string? Category,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public record CreateCannedResponseRequest(
    Guid? CompanyId,
    string Title,
    string Body,
    string? Category,
    int SortOrder);

public record UpdateCannedResponseRequest(
    string? Title,
    string? Body,
    string? Category,
    int? SortOrder,
    bool? IsActive);
