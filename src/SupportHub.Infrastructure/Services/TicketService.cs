namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class TicketService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUserService,
    IAuditService _auditService,
    ILogger<TicketService> _logger) : ITicketService
{
    private static readonly Dictionary<TicketStatus, HashSet<TicketStatus>> ValidTransitions = new()
    {
        [TicketStatus.New] = [TicketStatus.Open, TicketStatus.Pending, TicketStatus.Closed],
        [TicketStatus.Open] = [TicketStatus.Pending, TicketStatus.OnHold, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Pending] = [TicketStatus.Open, TicketStatus.OnHold, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.OnHold] = [TicketStatus.Open, TicketStatus.Pending, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Resolved] = [TicketStatus.Open, TicketStatus.Closed],
        [TicketStatus.Closed] = [TicketStatus.Open],
    };

    public async Task<Result<TicketDto>> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        if (!await _currentUserService.HasAccessToCompanyAsync(request.CompanyId, ct))
            return Result<TicketDto>.Failure("Access denied.");

        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var prefix = $"TKT-{today}-";
        var lastNumber = await _context.Tickets
            .IgnoreQueryFilters()
            .Where(t => t.TicketNumber.StartsWith(prefix))
            .OrderByDescending(t => t.TicketNumber)
            .Select(t => t.TicketNumber)
            .FirstOrDefaultAsync(ct);
        var seq = lastNumber is null ? 1 : int.Parse(lastNumber[^4..]) + 1;
        var ticketNumber = $"{prefix}{seq:D4}";

        var ticket = new Ticket
        {
            CompanyId = request.CompanyId,
            TicketNumber = ticketNumber,
            Subject = request.Subject,
            Description = request.Description,
            Priority = request.Priority,
            Source = request.Source,
            RequesterEmail = request.RequesterEmail,
            RequesterName = request.RequesterName,
            System = request.System,
            IssueType = request.IssueType,
            Status = TicketStatus.New,
        };

        _context.Tickets.Add(ticket);

        if (request.Tags is { Count: > 0 })
        {
            foreach (var tag in request.Tags)
            {
                var normalized = tag.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(normalized))
                {
                    _context.TicketTags.Add(new TicketTag
                    {
                        TicketId = ticket.Id,
                        Tag = normalized,
                    });
                }
            }
        }

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Created", "Ticket", ticket.Id.ToString(),
            newValues: new { ticket.TicketNumber, ticket.Subject, ticket.CompanyId }, ct: ct);

        _logger.LogInformation("Created ticket {TicketNumber} for company {CompanyId}", ticketNumber, request.CompanyId);

        return await GetTicketByIdAsync(ticket.Id, ct);
    }

    public async Task<Result<TicketDto>> GetTicketByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Company)
            .Include(t => t.AssignedAgent)
            .Include(t => t.Tags)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (ticket is null)
            return Result<TicketDto>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<TicketDto>.Failure("Access denied.");

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<PagedResult<TicketSummaryDto>>> GetTicketsAsync(TicketFilterRequest filter, CancellationToken ct = default)
    {
        var roles = await _currentUserService.GetUserRolesAsync(ct);
        var isSuperAdmin = roles.Any(r => r.Role == UserRole.SuperAdmin);

        var query = _context.Tickets
            .AsNoTracking()
            .AsQueryable();

        if (!isSuperAdmin)
        {
            var accessibleIds = roles.Select(r => r.CompanyId).ToHashSet();
            query = query.Where(t => accessibleIds.Contains(t.CompanyId));
        }

        if (filter.CompanyId.HasValue)
            query = query.Where(t => t.CompanyId == filter.CompanyId.Value);

        if (filter.Status.HasValue)
            query = query.Where(t => t.Status == filter.Status.Value);

        if (filter.Priority.HasValue)
            query = query.Where(t => t.Priority == filter.Priority.Value);

        if (filter.AssignedAgentId.HasValue)
            query = query.Where(t => t.AssignedAgentId == filter.AssignedAgentId.Value);

        if (filter.DateFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(t => t.CreatedAt <= filter.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(t =>
                t.Subject.Contains(term) ||
                t.RequesterName.Contains(term) ||
                t.RequesterEmail.Contains(term) ||
                t.TicketNumber.Contains(term));
        }

        if (filter.Tags is { Count: > 0 })
            query = query.Where(t => t.Tags.Any(tag => filter.Tags.Contains(tag.Tag)));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(t => new TicketSummaryDto(
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.Status,
                t.Priority,
                t.Source,
                t.RequesterName,
                t.RequesterEmail,
                t.Company.Name,
                t.AssignedAgent != null ? t.AssignedAgent.DisplayName : null,
                t.System,
                t.IssueType,
                t.Messages.Count,
                t.CreatedAt,
                t.UpdatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<TicketSummaryDto>>.Success(
            new PagedResult<TicketSummaryDto>(items, totalCount, filter.Page, filter.PageSize));
    }

    public async Task<Result<TicketDto>> UpdateTicketAsync(Guid id, UpdateTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return Result<TicketDto>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<TicketDto>.Failure("Access denied.");

        var oldValues = new { ticket.Subject, ticket.Description, ticket.Priority, ticket.System, ticket.IssueType };

        if (request.Subject is not null) ticket.Subject = request.Subject;
        if (request.Description is not null) ticket.Description = request.Description;
        if (request.Priority.HasValue) ticket.Priority = request.Priority.Value;
        if (request.System is not null) ticket.System = request.System;
        if (request.IssueType is not null) ticket.IssueType = request.IssueType;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Updated", "Ticket", ticket.Id.ToString(),
            oldValues: oldValues,
            newValues: new { ticket.Subject, ticket.Description, ticket.Priority, ticket.System, ticket.IssueType },
            ct: ct);

        return await GetTicketByIdAsync(ticket.Id, ct);
    }

    public async Task<Result<bool>> AssignTicketAsync(Guid ticketId, Guid agentId, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
            return Result<bool>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<bool>.Failure("Access denied.");

        var oldAgentId = ticket.AssignedAgentId;
        ticket.AssignedAgentId = agentId;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("AgentAssigned", "Ticket", ticket.Id.ToString(),
            oldValues: new { AssignedAgentId = oldAgentId },
            newValues: new { AssignedAgentId = agentId },
            ct: ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ChangeStatusAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
            return Result<bool>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<bool>.Failure("Access denied.");

        if (!ValidTransitions.TryGetValue(ticket.Status, out var allowed) || !allowed.Contains(newStatus))
            return Result<bool>.Failure($"Invalid status transition from {ticket.Status} to {newStatus}.");

        var oldStatus = ticket.Status;
        ticket.Status = newStatus;

        switch (newStatus)
        {
            case TicketStatus.Resolved:
                ticket.ResolvedAt = DateTimeOffset.UtcNow;
                break;
            case TicketStatus.Closed:
                ticket.ClosedAt = DateTimeOffset.UtcNow;
                break;
            case TicketStatus.Open when oldStatus is TicketStatus.Resolved or TicketStatus.Closed:
                ticket.ResolvedAt = null;
                ticket.ClosedAt = null;
                break;
        }

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("StatusChanged", "Ticket", ticket.Id.ToString(),
            oldValues: new { Status = oldStatus },
            newValues: new { Status = newStatus },
            ct: ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ChangePriorityAsync(Guid ticketId, TicketPriority newPriority, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
            return Result<bool>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<bool>.Failure("Access denied.");

        var oldPriority = ticket.Priority;
        ticket.Priority = newPriority;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("PriorityChanged", "Ticket", ticket.Id.ToString(),
            oldValues: new { Priority = oldPriority },
            newValues: new { Priority = newPriority },
            ct: ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteTicketAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (ticket is null)
            return Result<bool>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<bool>.Failure("Access denied.");

        ticket.IsDeleted = true;
        ticket.DeletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync("Deleted", "Ticket", ticket.Id.ToString(), ct: ct);

        return Result<bool>.Success(true);
    }

    private static TicketDto MapToDto(Ticket t) => new(
        t.Id,
        t.CompanyId,
        t.Company?.Name ?? string.Empty,
        t.TicketNumber,
        t.Subject,
        t.Description,
        t.Status,
        t.Priority,
        t.Source,
        t.RequesterEmail,
        t.RequesterName,
        t.AssignedAgentId,
        t.AssignedAgent?.DisplayName,
        t.System,
        t.IssueType,
        t.FirstResponseAt,
        t.ResolvedAt,
        t.ClosedAt,
        t.AiClassification,
        t.Tags.Select(tag => new TicketTagDto(tag.Id, tag.TicketId, tag.Tag)).ToList(),
        t.Attachments.Select(a => new TicketAttachmentDto(a.Id, a.TicketId, a.TicketMessageId, a.FileName, a.OriginalFileName, a.ContentType, a.FileSize, a.CreatedAt)).ToList(),
        t.CreatedAt,
        t.UpdatedAt);
}
