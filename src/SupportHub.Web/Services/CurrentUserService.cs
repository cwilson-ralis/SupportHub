namespace SupportHub.Web.Services;

using Microsoft.EntityFrameworkCore;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SupportHubDbContext _dbContext;
    private IReadOnlyList<UserCompanyRole>? _cachedRoles;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        SupportHubDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

    public string? DisplayName =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("name")?.Value;

    public string? Email =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("preferred_username")?.Value
            ?? _httpContextAccessor.HttpContext?.User
            .FindFirst("email")?.Value;

    public async Task<IReadOnlyList<UserCompanyRole>> GetUserRolesAsync(
        CancellationToken ct = default)
    {
        if (_cachedRoles is not null)
            return _cachedRoles;

        if (UserId is null)
            return [];

        var user = await _dbContext.ApplicationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AzureAdObjectId == UserId, ct);

        if (user is null)
            return [];

        _cachedRoles = await _dbContext.UserCompanyRoles
            .AsNoTracking()
            .Where(r => r.UserId == user.Id)
            .Include(r => r.Company)
            .ToListAsync(ct);

        return _cachedRoles;
    }

    public async Task<bool> HasAccessToCompanyAsync(
        Guid companyId, CancellationToken ct = default)
    {
        var roles = await GetUserRolesAsync(ct);

        // SuperAdmin has access to all companies
        if (roles.Any(r => r.Role == Domain.Enums.UserRole.SuperAdmin))
            return true;

        return roles.Any(r => r.CompanyId == companyId);
    }
}
