namespace SupportHub.Application.DTOs;

public record CompanyDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    string? Description,
    DateTimeOffset CreatedAt,
    int DivisionCount);

public record CreateCompanyRequest(
    string Name,
    string Code,
    string? Description);

public record UpdateCompanyRequest(
    string Name,
    string Code,
    bool IsActive,
    string? Description);
