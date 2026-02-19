namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Domain.Enums;

public interface IUserService
{
    Task<Result<PagedResult<UserDto>>> GetUsersAsync(
        int page, int pageSize, string? search = null, CancellationToken ct = default);

    Task<Result<UserDto>> GetUserByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<Result<UserDto>> SyncUserFromAzureAdAsync(
        string azureAdObjectId, CancellationToken ct = default);

    Task<Result<bool>> AssignRoleAsync(
        Guid userId, Guid companyId, UserRole role, CancellationToken ct = default);

    Task<Result<bool>> RemoveRoleAsync(
        Guid userId, Guid companyId, UserRole role, CancellationToken ct = default);
}
