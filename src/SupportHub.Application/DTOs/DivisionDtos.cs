namespace SupportHub.Application.DTOs;

public record DivisionDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CreateDivisionRequest(
    string Name);

public record UpdateDivisionRequest(
    string Name,
    bool IsActive);
