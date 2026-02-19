namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class CompanyService(
    SupportHubDbContext _context,
    IAuditService _auditService,
    ICurrentUserService _currentUserService) : ICompanyService
{

    public async Task<Result<PagedResult<CompanyDto>>> GetCompaniesAsync(
        int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        var roles = await _currentUserService.GetUserRolesAsync(ct);
        var isSuperAdmin = roles.Any(r => r.Role == UserRole.SuperAdmin);

        var query = _context.Companies.AsNoTracking();

        if (!isSuperAdmin)
        {
            var accessibleIds = roles.Select(r => r.CompanyId).ToHashSet();
            query = query.Where(c => accessibleIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CompanyDto(
                c.Id,
                c.Name,
                c.Code,
                c.IsActive,
                c.Description,
                c.CreatedAt,
                c.Divisions.Count(d => !d.IsDeleted)))
            .ToListAsync(ct);

        return Result<PagedResult<CompanyDto>>.Success(
            new PagedResult<CompanyDto>(items, total, page, pageSize));
    }

    public async Task<Result<CompanyDto>> GetCompanyByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        if (!await _currentUserService.HasAccessToCompanyAsync(id, ct))
            return Result<CompanyDto>.Failure("Access denied.");

        var company = await _context.Companies
            .AsNoTracking()
            .Include(c => c.Divisions.Where(d => !d.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (company is null)
            return Result<CompanyDto>.Failure("Company not found.");

        return Result<CompanyDto>.Success(ToDto(company));
    }

    public async Task<Result<CompanyDto>> CreateCompanyAsync(
        CreateCompanyRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<CompanyDto>.Failure("Company name is required.");

        if (string.IsNullOrWhiteSpace(request.Code))
            return Result<CompanyDto>.Failure("Company code is required.");

        if (await _context.Companies.AnyAsync(
                c => c.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase), ct))
            return Result<CompanyDto>.Failure("A company with this code already exists.");

        var company = new Company
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim().ToUpper(),
            Description = request.Description?.Trim()
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Create", "Company", company.Id.ToString(),
            newValues: new { company.Name, company.Code }, ct: ct);

        return Result<CompanyDto>.Success(ToDto(company));
    }

    public async Task<Result<CompanyDto>> UpdateCompanyAsync(
        Guid id, UpdateCompanyRequest request, CancellationToken ct = default)
    {
        if (!await _currentUserService.HasAccessToCompanyAsync(id, ct))
            return Result<CompanyDto>.Failure("Access denied.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company is null)
            return Result<CompanyDto>.Failure("Company not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<CompanyDto>.Failure("Company name is required.");

        if (string.IsNullOrWhiteSpace(request.Code))
            return Result<CompanyDto>.Failure("Company code is required.");

        if (await _context.Companies.AnyAsync(
                c => c.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase) && c.Id != id, ct))
            return Result<CompanyDto>.Failure("A company with this code already exists.");

        var oldValues = new { company.Name, company.Code, company.IsActive, company.Description };

        company.Name = request.Name.Trim();
        company.Code = request.Code.Trim().ToUpper();
        company.IsActive = request.IsActive;
        company.Description = request.Description?.Trim();

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Update", "Company", company.Id.ToString(),
            oldValues: oldValues,
            newValues: new { company.Name, company.Code, company.IsActive, company.Description },
            ct: ct);

        return Result<CompanyDto>.Success(ToDto(company));
    }

    public async Task<Result<bool>> DeleteCompanyAsync(
        Guid id, CancellationToken ct = default)
    {
        if (!await _currentUserService.HasAccessToCompanyAsync(id, ct))
            return Result<bool>.Failure("Access denied.");

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company is null)
            return Result<bool>.Failure("Company not found.");

        company.IsDeleted = true;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Delete", "Company", company.Id.ToString(), ct: ct);

        return Result<bool>.Success(true);
    }

    private static CompanyDto ToDto(Company company) => new(
        company.Id,
        company.Name,
        company.Code,
        company.IsActive,
        company.Description,
        company.CreatedAt,
        company.Divisions?.Count(d => !d.IsDeleted) ?? 0);
}
