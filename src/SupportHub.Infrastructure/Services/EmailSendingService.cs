namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public class EmailSendingService(
    SupportHubDbContext _context,
    IGraphClientFactory _graphClientFactory,
    ITicketMessageService _ticketMessageService,
    IAuditService _auditService,
    ILogger<EmailSendingService> _logger) : IEmailSendingService
{
    public async Task<Result<bool>> SendReplyAsync(Guid ticketId, string body, string? htmlBody, IReadOnlyList<Guid>? attachmentIds, CancellationToken ct = default)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Company)
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.IsDeleted, ct);
        if (ticket is null)
            return Result<bool>.Failure("Ticket not found.");

        var emailConfig = await _context.EmailConfigurations
            .FirstOrDefaultAsync(c => c.CompanyId == ticket.CompanyId && c.IsActive && !c.IsDeleted, ct);
        if (emailConfig is null)
            return Result<bool>.Failure("No active email configuration found for this company.");

        var subject = $"Re: [{ticket.TicketNumber}] {ticket.Subject}";
        var graphClient = _graphClientFactory.CreateClient();

        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody
            {
                Content = htmlBody ?? body,
                ContentType = htmlBody is not null ? BodyType.Html : BodyType.Text,
            },
            ToRecipients =
            [
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = ticket.RequesterEmail,
                        Name = ticket.RequesterName,
                    },
                },
            ],
            InternetMessageHeaders =
            [
                new InternetMessageHeader
                {
                    Name = "X-SupportHub-TicketId",
                    Value = ticketId.ToString(),
                },
            ],
        };

        // Add file attachments if specified
        if (attachmentIds is { Count: > 0 })
        {
            var ticketAttachments = await _context.TicketAttachments
                .Where(a => attachmentIds.Contains(a.Id) && a.TicketId == ticketId)
                .ToListAsync(ct);

            var graphAttachments = new List<Attachment>();
            foreach (var att in ticketAttachments)
            {
                graphAttachments.Add(new FileAttachment
                {
                    Name = att.OriginalFileName,
                    ContentType = att.ContentType,
                    ContentBytes = await File.ReadAllBytesAsync(att.FileName, ct),
                });
            }

            message.Attachments = graphAttachments;
        }

        try
        {
            await graphClient.Users[emailConfig.SharedMailboxAddress].SendMail.PostAsync(
                new SendMailPostRequestBody { Message = message, SaveToSentItems = true },
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email reply for ticket {TicketId}", ticketId);
            return Result<bool>.Failure($"Failed to send email: {ex.Message}");
        }

        // Record outbound message
        var msgRequest = new CreateTicketMessageRequest(
            MessageDirection.Outbound,
            emailConfig.SharedMailboxAddress,
            emailConfig.DisplayName,
            body,
            htmlBody);
        await _ticketMessageService.AddMessageAsync(ticketId, msgRequest, ct);

        await _auditService.LogAsync("EmailReplySent", "Ticket", ticketId.ToString(),
            newValues: new { Subject = subject, To = ticket.RequesterEmail }, ct: ct);

        _logger.LogInformation("Sent email reply for ticket {TicketId} to {Email}", ticketId, ticket.RequesterEmail);
        return Result<bool>.Success(true);
    }
}
