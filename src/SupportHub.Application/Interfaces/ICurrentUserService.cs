namespace SupportHub.Application.Interfaces;

using SupportHub.Domain.Entities;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? DisplayName { get; }
    string? Email { get; }
    Task<IReadOnlyList<UserCompanyRole>> GetUserRolesAsync(CancellationToken ct = default);
    Task<bool> HasAccessToCompanyAsync(Guid companyId, CancellationToken ct = default);
}
