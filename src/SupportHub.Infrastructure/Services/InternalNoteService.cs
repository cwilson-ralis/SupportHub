namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class InternalNoteService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUserService,
    ILogger<InternalNoteService> _logger) : IInternalNoteService
{
    public async Task<Result<InternalNoteDto>> AddNoteAsync(Guid ticketId, CreateInternalNoteRequest request, CancellationToken ct = default)
    {
        var roles = await _currentUserService.GetUserRolesAsync(ct);
        var hasValidRole = roles.Any(r => r.Role is UserRole.SuperAdmin or UserRole.Admin or UserRole.Agent);

        if (!hasValidRole)
            return Result<InternalNoteDto>.Failure("Unauthorized: only agents can add internal notes.");

        var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
            return Result<InternalNoteDto>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<InternalNoteDto>.Failure("Access denied.");

        var note = new InternalNote
        {
            TicketId = ticketId,
            AuthorId = Guid.Parse(_currentUserService.UserId!),
            Body = request.Body,
        };

        _context.InternalNotes.Add(note);
        await _context.SaveChangesAsync(ct);

        var author = await _context.ApplicationUsers.FindAsync([note.AuthorId], ct);

        _logger.LogInformation("Added internal note to ticket {TicketId} by {AuthorId}", ticketId, note.AuthorId);

        return Result<InternalNoteDto>.Success(new InternalNoteDto(
            note.Id,
            note.TicketId,
            note.AuthorId,
            author?.DisplayName ?? "Unknown",
            note.Body,
            note.CreatedAt));
    }

    public async Task<Result<IReadOnlyList<InternalNoteDto>>> GetNotesAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
            return Result<IReadOnlyList<InternalNoteDto>>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<IReadOnlyList<InternalNoteDto>>.Failure("Access denied.");

        var notes = await _context.InternalNotes
            .AsNoTracking()
            .Include(n => n.Author)
            .Where(n => n.TicketId == ticketId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new InternalNoteDto(
                n.Id,
                n.TicketId,
                n.AuthorId,
                n.Author.DisplayName ?? "Unknown",
                n.Body,
                n.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<InternalNoteDto>>.Success(notes);
    }
}
