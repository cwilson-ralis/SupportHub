namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class EmailConfigurationService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUserService,
    IAuditService _auditService,
    ILogger<EmailConfigurationService> _logger) : IEmailConfigurationService
{
    public async Task<Result<EmailConfigurationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.EmailConfigurations
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

        if (entity is null)
            return Result<EmailConfigurationDto>.Failure("Email configuration not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(entity.CompanyId, ct))
            return Result<EmailConfigurationDto>.Failure("Access denied.");

        return Result<EmailConfigurationDto>.Success(MapToDto(entity));
    }

    public async Task<Result<IReadOnlyList<EmailConfigurationDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await _currentUserService.GetUserRolesAsync(ct);
        var isSuperAdmin = roles.Any(r => r.Role == UserRole.SuperAdmin);

        var query = _context.EmailConfigurations
            .Include(e => e.Company)
            .Where(e => !e.IsDeleted);

        if (!isSuperAdmin)
        {
            var accessibleIds = roles.Select(r => r.CompanyId).ToHashSet();
            query = query.Where(e => accessibleIds.Contains(e.CompanyId));
        }

        var entities = await query
            .OrderBy(e => e.Company.Name)
            .ThenBy(e => e.SharedMailboxAddress)
            .ToListAsync(ct);

        return Result<IReadOnlyList<EmailConfigurationDto>>.Success(
            entities.Select(MapToDto).ToList());
    }

    public async Task<Result<IReadOnlyList<EmailConfigurationDto>>> GetActiveAsync(CancellationToken ct = default)
    {
        var roles = await _currentUserService.GetUserRolesAsync(ct);
        var isSuperAdmin = roles.Any(r => r.Role == UserRole.SuperAdmin);

        var query = _context.EmailConfigurations
            .Include(e => e.Company)
            .Where(e => !e.IsDeleted && e.IsActive);

        if (!isSuperAdmin)
        {
            var accessibleIds = roles.Select(r => r.CompanyId).ToHashSet();
            query = query.Where(e => accessibleIds.Contains(e.CompanyId));
        }

        var entities = await query
            .OrderBy(e => e.Company.Name)
            .ThenBy(e => e.SharedMailboxAddress)
            .ToListAsync(ct);

        return Result<IReadOnlyList<EmailConfigurationDto>>.Success(
            entities.Select(MapToDto).ToList());
    }

    public async Task<Result<EmailConfigurationDto>> CreateAsync(CreateEmailConfigurationRequest request, CancellationToken ct = default)
    {
        if (!await _currentUserService.HasAccessToCompanyAsync(request.CompanyId, ct))
            return Result<EmailConfigurationDto>.Failure("Access denied.");

        var entity = new EmailConfiguration
        {
            CompanyId = request.CompanyId,
            SharedMailboxAddress = request.SharedMailboxAddress,
            DisplayName = request.DisplayName,
            IsActive = request.IsActive,
            PollingIntervalMinutes = request.PollingIntervalMinutes,
            AutoCreateTickets = request.AutoCreateTickets,
            DefaultPriority = request.DefaultPriority,
        };

        _context.EmailConfigurations.Add(entity);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Created", "EmailConfiguration", entity.Id.ToString(),
            newValues: new { entity.SharedMailboxAddress, entity.CompanyId }, ct: ct);

        _logger.LogInformation("Created email configuration {Id} for company {CompanyId}", entity.Id, entity.CompanyId);

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<Result<EmailConfigurationDto>> UpdateAsync(Guid id, UpdateEmailConfigurationRequest request, CancellationToken ct = default)
    {
        var entity = await _context.EmailConfigurations
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

        if (entity is null)
            return Result<EmailConfigurationDto>.Failure("Email configuration not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(entity.CompanyId, ct))
            return Result<EmailConfigurationDto>.Failure("Access denied.");

        var oldValues = new { entity.DisplayName, entity.IsActive, entity.PollingIntervalMinutes, entity.AutoCreateTickets, entity.DefaultPriority };

        if (request.DisplayName is not null) entity.DisplayName = request.DisplayName;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.PollingIntervalMinutes.HasValue) entity.PollingIntervalMinutes = request.PollingIntervalMinutes.Value;
        if (request.AutoCreateTickets.HasValue) entity.AutoCreateTickets = request.AutoCreateTickets.Value;
        if (request.DefaultPriority.HasValue) entity.DefaultPriority = request.DefaultPriority.Value;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Updated", "EmailConfiguration", entity.Id.ToString(),
            oldValues: oldValues,
            newValues: new { entity.DisplayName, entity.IsActive, entity.PollingIntervalMinutes, entity.AutoCreateTickets, entity.DefaultPriority },
            ct: ct);

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.EmailConfigurations
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

        if (entity is null)
            return Result<bool>.Failure("Email configuration not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(entity.CompanyId, ct))
            return Result<bool>.Failure("Access denied.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Deleted", "EmailConfiguration", entity.Id.ToString(), ct: ct);

        _logger.LogInformation("Deleted email configuration {Id}", id);

        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyList<EmailProcessingLogDto>>> GetLogsAsync(Guid emailConfigurationId, int count = 50, CancellationToken ct = default)
    {
        var config = await _context.EmailConfigurations
            .FirstOrDefaultAsync(e => e.Id == emailConfigurationId && !e.IsDeleted, ct);

        if (config is null)
            return Result<IReadOnlyList<EmailProcessingLogDto>>.Failure("Email configuration not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(config.CompanyId, ct))
            return Result<IReadOnlyList<EmailProcessingLogDto>>.Failure("Access denied.");

        var logs = await _context.EmailProcessingLogs
            .Where(l => l.EmailConfigurationId == emailConfigurationId)
            .OrderByDescending(l => l.ProcessedAt)
            .Take(count)
            .Select(l => new EmailProcessingLogDto(
                l.Id,
                l.EmailConfigurationId,
                l.ExternalMessageId,
                l.Subject,
                l.SenderEmail,
                l.ProcessingResult,
                l.TicketId,
                l.ErrorMessage,
                l.ProcessedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<EmailProcessingLogDto>>.Success(logs);
    }

    private static EmailConfigurationDto MapToDto(EmailConfiguration e) => new(
        e.Id, e.CompanyId, e.Company?.Name ?? string.Empty,
        e.SharedMailboxAddress, e.DisplayName, e.IsActive, e.PollingIntervalMinutes,
        e.LastPolledAt, e.LastPolledMessageId, e.AutoCreateTickets, e.DefaultPriority,
        e.CreatedAt, e.UpdatedAt);
}
