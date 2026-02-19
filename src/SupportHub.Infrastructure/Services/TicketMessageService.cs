namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class TicketMessageService(
    SupportHubDbContext _context,
    ICurrentUserService _currentUserService,
    ILogger<TicketMessageService> _logger) : ITicketMessageService
{
    public async Task<Result<TicketMessageDto>> AddMessageAsync(Guid ticketId, CreateTicketMessageRequest request, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket is null)
            return Result<TicketMessageDto>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<TicketMessageDto>.Failure("Access denied.");

        var message = new TicketMessage
        {
            TicketId = ticketId,
            Direction = request.Direction,
            SenderEmail = request.SenderEmail,
            SenderName = request.SenderName,
            Body = request.Body,
            HtmlBody = request.HtmlBody,
        };

        if (request.Direction == MessageDirection.Outbound)
        {
            if (ticket.FirstResponseAt is null)
                ticket.FirstResponseAt = DateTimeOffset.UtcNow;

            if (ticket.Status == TicketStatus.New)
                ticket.Status = TicketStatus.Open;
        }

        _context.TicketMessages.Add(message);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added {Direction} message to ticket {TicketId}", request.Direction, ticketId);

        return Result<TicketMessageDto>.Success(new TicketMessageDto(
            message.Id,
            message.TicketId,
            message.Direction,
            message.SenderEmail,
            message.SenderName,
            message.Body,
            message.HtmlBody,
            [],
            message.CreatedAt));
    }

    public async Task<Result<IReadOnlyList<TicketMessageDto>>> GetMessagesAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
            return Result<IReadOnlyList<TicketMessageDto>>.Failure("Ticket not found.");

        if (!await _currentUserService.HasAccessToCompanyAsync(ticket.CompanyId, ct))
            return Result<IReadOnlyList<TicketMessageDto>>.Failure("Access denied.");

        var messages = await _context.TicketMessages
            .AsNoTracking()
            .Include(m => m.Attachments)
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageDto(
                m.Id,
                m.TicketId,
                m.Direction,
                m.SenderEmail,
                m.SenderName,
                m.Body,
                m.HtmlBody,
                m.Attachments.Select(a => new TicketAttachmentDto(
                    a.Id, a.TicketId, a.TicketMessageId, a.FileName, a.OriginalFileName, a.ContentType, a.FileSize, a.CreatedAt)).ToList(),
                m.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<TicketMessageDto>>.Success(messages);
    }
}
