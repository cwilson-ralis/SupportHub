namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

public class QueueService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUser,
    IAuditService _audit,
    ILogger<QueueService> _logger) : IQueueService
{
    public async Task<Result<PagedResult<QueueDto>>> GetQueuesAsync(Guid companyId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await _currentUser.HasAccessToCompanyAsync(companyId, ct))
            return Result<PagedResult<QueueDto>>.Failure("Access denied.");

        var query = _context.Queues
            .AsNoTracking()
            .Where(q => q.CompanyId == companyId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(q => q.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new
            {
                q.Id,
                q.CompanyId,
                CompanyName = q.Company.Name,
                q.Name,
                q.Description,
                q.IsDefault,
                q.IsActive,
                q.CreatedAt,
                q.UpdatedAt,
                TicketCount = q.Tickets.Count(t => !t.IsDeleted),
            })
            .ToListAsync(ct);

        var dtos = items.Select(q => new QueueDto(
            q.Id,
            q.CompanyId,
            q.CompanyName,
            q.Name,
            q.Description,
            q.IsDefault,
            q.IsActive,
            q.TicketCount,
            q.CreatedAt,
            q.UpdatedAt)).ToList();

        return Result<PagedResult<QueueDto>>.Success(
            new PagedResult<QueueDto>(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<QueueDto>> GetQueueByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.Queues
            .AsNoTracking()
            .Where(q => q.Id == id)
            .Select(q => new
            {
                q.Id,
                q.CompanyId,
                CompanyName = q.Company.Name,
                q.Name,
                q.Description,
                q.IsDefault,
                q.IsActive,
                q.CreatedAt,
                q.UpdatedAt,
                TicketCount = q.Tickets.Count(t => !t.IsDeleted),
            })
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return Result<QueueDto>.Failure("Queue not found.");

        if (!await _currentUser.HasAccessToCompanyAsync(item.CompanyId, ct))
            return Result<QueueDto>.Failure("Access denied.");

        return Result<QueueDto>.Success(new QueueDto(
            item.Id,
            item.CompanyId,
            item.CompanyName,
            item.Name,
            item.Description,
            item.IsDefault,
            item.IsActive,
            item.TicketCount,
            item.CreatedAt,
            item.UpdatedAt));
    }

    public async Task<Result<QueueDto>> CreateQueueAsync(CreateQueueRequest request, CancellationToken ct = default)
    {
        if (!await _currentUser.HasAccessToCompanyAsync(request.CompanyId, ct))
            return Result<QueueDto>.Failure("Access denied.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<QueueDto>.Failure("Queue name is required.");

        var nameExists = await _context.Queues
            .AnyAsync(q => q.CompanyId == request.CompanyId && q.Name == request.Name, ct);
        if (nameExists)
            return Result<QueueDto>.Failure($"A queue named '{request.Name}' already exists for this company.");

        if (request.IsDefault)
        {
            var existingDefault = await _context.Queues
                .Where(q => q.CompanyId == request.CompanyId && q.IsDefault)
                .ToListAsync(ct);
            foreach (var q in existingDefault)
                q.IsDefault = false;
        }

        var queue = new Queue
        {
            CompanyId = request.CompanyId,
            Name = request.Name,
            Description = request.Description,
            IsDefault = request.IsDefault,
            IsActive = true,
        };

        _context.Queues.Add(queue);
        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("Created", "Queue", queue.Id.ToString(),
            newValues: new { queue.Name, queue.CompanyId, queue.IsDefault }, ct: ct);

        _logger.LogInformation("Created queue {QueueName} for company {CompanyId}", queue.Name, queue.CompanyId);

        return await GetQueueByIdAsync(queue.Id, ct);
    }

    public async Task<Result<QueueDto>> UpdateQueueAsync(Guid id, UpdateQueueRequest request, CancellationToken ct = default)
    {
        var queue = await _context.Queues.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (queue is null)
            return Result<QueueDto>.Failure("Queue not found.");

        if (!await _currentUser.HasAccessToCompanyAsync(queue.CompanyId, ct))
            return Result<QueueDto>.Failure("Access denied.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<QueueDto>.Failure("Queue name is required.");

        var nameExists = await _context.Queues
            .AnyAsync(q => q.CompanyId == queue.CompanyId && q.Name == request.Name && q.Id != id, ct);
        if (nameExists)
            return Result<QueueDto>.Failure($"A queue named '{request.Name}' already exists for this company.");

        if (request.IsDefault && !queue.IsDefault)
        {
            var existingDefault = await _context.Queues
                .Where(q => q.CompanyId == queue.CompanyId && q.IsDefault && q.Id != id)
                .ToListAsync(ct);
            foreach (var q in existingDefault)
                q.IsDefault = false;
        }

        var oldValues = new { queue.Name, queue.Description, queue.IsDefault, queue.IsActive };

        queue.Name = request.Name;
        queue.Description = request.Description;
        queue.IsDefault = request.IsDefault;
        queue.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("Updated", "Queue", queue.Id.ToString(),
            oldValues: oldValues,
            newValues: new { queue.Name, queue.Description, queue.IsDefault, queue.IsActive },
            ct: ct);

        _logger.LogInformation("Updated queue {QueueId} ({QueueName})", queue.Id, queue.Name);

        return await GetQueueByIdAsync(queue.Id, ct);
    }

    public async Task<Result<bool>> DeleteQueueAsync(Guid id, CancellationToken ct = default)
    {
        var queue = await _context.Queues.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (queue is null)
            return Result<bool>.Failure("Queue not found.");

        if (!await _currentUser.HasAccessToCompanyAsync(queue.CompanyId, ct))
            return Result<bool>.Failure("Access denied.");

        var hasActiveTickets = await _context.Tickets
            .AnyAsync(t => t.QueueId == id && !t.IsDeleted, ct);
        if (hasActiveTickets)
            return Result<bool>.Failure("Cannot delete a queue that has tickets. Reassign tickets first.");

        queue.IsDeleted = true;
        queue.DeletedAt = DateTimeOffset.UtcNow;
        queue.DeletedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(ct);

        await _audit.LogAsync("Deleted", "Queue", queue.Id.ToString(), ct: ct);

        _logger.LogInformation("Deleted queue {QueueId} ({QueueName})", queue.Id, queue.Name);

        return Result<bool>.Success(true);
    }
}
