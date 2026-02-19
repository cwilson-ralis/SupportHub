namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class UserService : IUserService
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public UserService(
        SupportHubDbContext context,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<Result<PagedResult<UserDto>>> GetUsersAsync(
        int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        var query = _context.ApplicationUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.DisplayName.Contains(search) ||
                u.Email.Contains(search));

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.UserCompanyRoles.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Company)
            .ToListAsync(ct);

        var dtos = users.Select(ToDto).ToList();
        return Result<PagedResult<UserDto>>.Success(
            new PagedResult<UserDto>(dtos, total, page, pageSize));
    }

    public async Task<Result<UserDto>> GetUserByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var user = await _context.ApplicationUsers
            .AsNoTracking()
            .Include(u => u.UserCompanyRoles.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Company)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return Result<UserDto>.Failure("User not found.");

        return Result<UserDto>.Success(ToDto(user));
    }

    public async Task<Result<UserDto>> SyncUserFromAzureAdAsync(
        string azureAdObjectId, CancellationToken ct = default)
    {
        var displayName = _currentUserService.DisplayName ?? "Unknown";
        var email = _currentUserService.Email ?? string.Empty;

        var user = await _context.ApplicationUsers
            .Include(u => u.UserCompanyRoles.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Company)
            .FirstOrDefaultAsync(u => u.AzureAdObjectId == azureAdObjectId, ct);

        if (user is null)
        {
            user = new ApplicationUser
            {
                AzureAdObjectId = azureAdObjectId,
                Email = email,
                DisplayName = displayName
            };
            _context.ApplicationUsers.Add(user);
            await _context.SaveChangesAsync(ct);
            await _auditService.LogAsync("Create", "ApplicationUser", user.Id.ToString(),
                newValues: new { user.AzureAdObjectId, user.Email, user.DisplayName }, ct: ct);
        }
        else
        {
            var changed = false;
            var oldValues = new { user.Email, user.DisplayName };

            if (user.Email != email) { user.Email = email; changed = true; }
            if (user.DisplayName != displayName) { user.DisplayName = displayName; changed = true; }

            if (changed)
            {
                await _context.SaveChangesAsync(ct);
                await _auditService.LogAsync("Update", "ApplicationUser", user.Id.ToString(),
                    oldValues: oldValues,
                    newValues: new { user.Email, user.DisplayName }, ct: ct);
            }
        }

        return Result<UserDto>.Success(ToDto(user));
    }

    public async Task<Result<bool>> AssignRoleAsync(
        Guid userId, Guid companyId, UserRole role, CancellationToken ct = default)
    {
        var user = await _context.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Result<bool>.Failure("User not found.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null)
            return Result<bool>.Failure("Company not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(companyId, ct))
            return Result<bool>.Failure("Access denied to this company.");

        var exists = await _context.UserCompanyRoles.AnyAsync(
            r => r.UserId == userId && r.CompanyId == companyId && r.Role == role, ct);
        if (exists)
            return Result<bool>.Failure("This role is already assigned to the user for this company.");

        var ucr = new UserCompanyRole
        {
            UserId = userId,
            CompanyId = companyId,
            Role = role
        };

        _context.UserCompanyRoles.Add(ucr);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("AssignRole", "UserCompanyRole", ucr.Id.ToString(),
            newValues: new { userId, companyId, Role = role.ToString() }, ct: ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> RemoveRoleAsync(
        Guid userId, Guid companyId, UserRole role, CancellationToken ct = default)
    {
        var ucr = await _context.UserCompanyRoles.FirstOrDefaultAsync(
            r => r.UserId == userId && r.CompanyId == companyId && r.Role == role, ct);

        if (ucr is null)
            return Result<bool>.Failure("Role assignment not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ucr.CompanyId, ct))
            return Result<bool>.Failure("Access denied to this company.");

        ucr.IsDeleted = true;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("RemoveRole", "UserCompanyRole", ucr.Id.ToString(),
            oldValues: new { userId, companyId, Role = role.ToString() }, ct: ct);

        return Result<bool>.Success(true);
    }

    private static UserDto ToDto(ApplicationUser user) => new(
        user.Id,
        user.AzureAdObjectId,
        user.Email,
        user.DisplayName,
        user.IsActive,
        user.CreatedAt,
        user.UserCompanyRoles?
            .Where(r => !r.IsDeleted)
            .Select(r => new UserCompanyRoleDto(
                r.Id,
                r.CompanyId,
                r.Company?.Name ?? string.Empty,
                r.Role.ToString()))
            .ToList() ?? []);
}
