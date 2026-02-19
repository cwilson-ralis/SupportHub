namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

public class TagService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUserService,
    ILogger<TagService> _logger) : ITagService
{
    public async Task<Result<TicketTagDto>> AddTagAsync(Guid ticketId, string tag, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
            return Result<TicketTagDto>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<TicketTagDto>.Failure("Access denied.");

        var normalized = tag.Trim().ToLowerInvariant();

        var exists = await _context.TicketTags.AnyAsync(t => t.TicketId == ticketId && t.Tag == normalized, ct);
        if (exists)
            return Result<TicketTagDto>.Failure("Tag already exists on this ticket.");

        var tagEntity = new TicketTag
        {
            TicketId = ticketId,
            Tag = normalized,
        };

        _context.TicketTags.Add(tagEntity);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added tag '{Tag}' to ticket {TicketId}", normalized, ticketId);

        return Result<TicketTagDto>.Success(new TicketTagDto(tagEntity.Id, tagEntity.TicketId, tagEntity.Tag));
    }

    public async Task<Result<bool>> RemoveTagAsync(Guid ticketId, string tag, CancellationToken ct = default)
    {
        var normalized = tag.Trim().ToLowerInvariant();

        var tagEntity = await _context.TicketTags
            .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.Tag == normalized, ct);

        if (tagEntity is null)
            return Result<bool>.Failure("Tag not found.");

        tagEntity.IsDeleted = true;
        tagEntity.DeletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Removed tag '{Tag}' from ticket {TicketId}", normalized, ticketId);

        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyList<string>>> GetPopularTagsAsync(Guid? companyId, int count = 20, CancellationToken ct = default)
    {
        var query = _context.TicketTags.AsQueryable();

        if (companyId.HasValue)
            query = query.Where(t => t.Ticket.CompanyId == companyId.Value);

        var tags = await query
            .GroupBy(t => t.Tag)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToListAsync(ct);

        return Result<IReadOnlyList<string>>.Success(tags);
    }
}
