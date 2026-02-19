namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

public class CannedResponseService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUserService,
    ILogger<CannedResponseService> _logger) : ICannedResponseService
{
    public async Task<Result<PagedResult<CannedResponseDto>>> GetCannedResponsesAsync(Guid? companyId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.CannedResponses
            .AsNoTracking()
            .Include(cr => cr.Company)
            .Where(cr => cr.IsActive);

        if (companyId.HasValue)
            query = query.Where(cr => cr.CompanyId == companyId.Value || cr.CompanyId == null);
        else
            query = query.Where(cr => cr.CompanyId == null);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(cr => cr.SortOrder)
            .ThenBy(cr => cr.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cr => new CannedResponseDto(
                cr.Id,
                cr.CompanyId,
                cr.Company != null ? cr.Company.Name : null,
                cr.Title,
                cr.Body,
                cr.Category,
                cr.SortOrder,
                cr.IsActive,
                cr.CreatedAt,
                cr.UpdatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<CannedResponseDto>>.Success(
            new PagedResult<CannedResponseDto>(items, totalCount, page, pageSize));
    }

    public async Task<Result<CannedResponseDto>> CreateCannedResponseAsync(CreateCannedResponseRequest request, CancellationToken ct = default)
    {
        if (request.CompanyId.HasValue)
        {
            if (!await _currentUserService.HasAccessToCompanyAsync(request.CompanyId.Value, ct))
                return Result<CannedResponseDto>.Failure("Access denied.");
        }

        var entity = new CannedResponse
        {
            CompanyId = request.CompanyId,
            Title = request.Title,
            Body = request.Body,
            Category = request.Category,
            SortOrder = request.SortOrder,
            IsActive = true,
        };

        _context.CannedResponses.Add(entity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created canned response {Id} with title '{Title}'", entity.Id, entity.Title);

        var company = request.CompanyId.HasValue
            ? await _context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CompanyId.Value, ct)
            : null;

        return Result<CannedResponseDto>.Success(new CannedResponseDto(
            entity.Id,
            entity.CompanyId,
            company?.Name,
            entity.Title,
            entity.Body,
            entity.Category,
            entity.SortOrder,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    public async Task<Result<CannedResponseDto>> UpdateCannedResponseAsync(Guid id, UpdateCannedResponseRequest request, CancellationToken ct = default)
    {
        var entity = await _context.CannedResponses
            .Include(cr => cr.Company)
            .FirstOrDefaultAsync(cr => cr.Id == id, ct);

        if (entity is null)
            return Result<CannedResponseDto>.Failure("Canned response not found.");

        if (entity.CompanyId.HasValue && !await _currentUserService.HasAccessToCompanyAsync(entity.CompanyId.Value, ct))
            return Result<CannedResponseDto>.Failure("Access denied.");

        if (request.Title is not null) entity.Title = request.Title;
        if (request.Body is not null) entity.Body = request.Body;
        if (request.Category is not null) entity.Category = request.Category;
        if (request.SortOrder.HasValue) entity.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated canned response {Id}", id);

        return Result<CannedResponseDto>.Success(new CannedResponseDto(
            entity.Id,
            entity.CompanyId,
            entity.Company?.Name,
            entity.Title,
            entity.Body,
            entity.Category,
            entity.SortOrder,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    public async Task<Result<bool>> DeleteCannedResponseAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.CannedResponses.FirstOrDefaultAsync(cr => cr.Id == id, ct);
        if (entity is null)
            return Result<bool>.Failure("Canned response not found.");

        if (entity.CompanyId.HasValue && !await _currentUserService.HasAccessToCompanyAsync(entity.CompanyId.Value, ct))
            return Result<bool>.Failure("Access denied.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted canned response {Id}", id);

        return Result<bool>.Success(true);
    }
}
