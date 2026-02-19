namespace SupportHub.Application.DTOs;

public record UserDto(
    Guid Id,
    string AzureAdObjectId,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<UserCompanyRoleDto> Roles);

public record UserCompanyRoleDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Role);
