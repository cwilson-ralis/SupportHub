namespace SupportHub.Infrastructure.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Domain.Enums;
using SupportHub.Infrastructure.Data;

public partial class EmailProcessingService(
    SupportHubDbContext _context,
    ITicketService _ticketService,
    ITicketMessageService _ticketMessageService,
    IAttachmentService _attachmentService,
    IAiClassificationService _aiClassificationService,
    ILogger<EmailProcessingService> _logger) : IEmailProcessingService
{
    [GeneratedRegex(@"TKT-\d{8}-\d{4}", RegexOptions.Compiled)]
    private static partial Regex TicketNumberPattern();

    public async Task<Result<Guid?>> ProcessInboundEmailAsync(InboundEmailMessage message, Guid emailConfigurationId, CancellationToken ct = default)
    {
        var config = await _context.EmailConfigurations
            .FirstOrDefaultAsync(c => c.Id == emailConfigurationId && !c.IsDeleted, ct);
        if (config is null)
            return Result<Guid?>.Failure("Email configuration not found.");

        // Check if already processed
        var alreadyProcessed = await _context.EmailProcessingLogs
            .AnyAsync(l => l.ExternalMessageId == message.ExternalMessageId, ct);
        if (alreadyProcessed)
        {
            _logger.LogDebug("Email {MessageId} already processed, skipping", message.ExternalMessageId);
            return Result<Guid?>.Success(null);
        }

        Guid? ticketId = null;
        string processingResult;

        try
        {
            Ticket? existingTicket = null;

            // Check X-SupportHub-TicketId header
            if (message.InternetHeaders.TryGetValue("X-SupportHub-TicketId", out var headerTicketId)
                && Guid.TryParse(headerTicketId, out var parsedTicketId))
            {
                existingTicket = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.Id == parsedTicketId && !t.IsDeleted, ct);
            }

            // Fallback: subject line pattern match
            if (existingTicket is null)
            {
                var match = TicketNumberPattern().Match(message.Subject);
                if (match.Success)
                {
                    var ticketNumber = match.Value;
                    existingTicket = await _context.Tickets
                        .FirstOrDefaultAsync(t => t.TicketNumber == ticketNumber && !t.IsDeleted, ct);
                }
            }

            // Defence-in-depth: ensure matched ticket belongs to the same company as the email config
            if (existingTicket is not null && existingTicket.CompanyId != config.CompanyId)
            {
                _logger.LogWarning("Email {MessageId} header/subject matched ticket {TicketId} from a different company — treating as new email",
                    message.ExternalMessageId, existingTicket.Id);
                existingTicket = null;
            }

            if (existingTicket is not null)
            {
                // Append to existing ticket
                var appendRequest = new CreateTicketMessageRequest(
                    MessageDirection.Inbound,
                    message.SenderEmail,
                    message.SenderName,
                    message.Body,
                    message.HtmlBody);
                var appendResult = await _ticketMessageService.AddMessageAsync(existingTicket.Id, appendRequest, ct);
                if (!appendResult.IsSuccess)
                    return Result<Guid?>.Failure(appendResult.Error!);

                ticketId = existingTicket.Id;

                // Save attachments
                foreach (var attachment in message.Attachments)
                    await _attachmentService.UploadAttachmentAsync(
                        existingTicket.Id, appendResult.Value!.Id, attachment.Content,
                        attachment.FileName, attachment.ContentType, attachment.Size, ct);

                processingResult = "Appended";
            }
            else if (config.AutoCreateTickets)
            {
                // Classify via AI
                var classifyResult = await _aiClassificationService.ClassifyAsync(
                    message.Subject, message.Body, config.CompanyId, ct);

                // Create new ticket
                var createRequest = new CreateTicketRequest(
                    config.CompanyId,
                    message.Subject,
                    message.Body,
                    config.DefaultPriority,
                    TicketSource.Email,
                    message.SenderEmail,
                    message.SenderName,
                    null,
                    null,
                    null);
                var createResult = await _ticketService.CreateTicketAsync(createRequest, ct);
                if (!createResult.IsSuccess)
                    return Result<Guid?>.Failure(createResult.Error!);

                ticketId = createResult.Value!.Id;

                // Store AI classification result
                if (classifyResult.IsSuccess)
                {
                    var ticket = await _context.Tickets.FindAsync([ticketId.Value], ct);
                    if (ticket is not null)
                    {
                        ticket.AiClassification = JsonSerializer.Serialize(classifyResult.Value);
                        await _context.SaveChangesAsync(ct);
                    }
                }

                // Save attachments
                foreach (var attachment in message.Attachments)
                    await _attachmentService.UploadAttachmentAsync(
                        ticketId.Value, null, attachment.Content,
                        attachment.FileName, attachment.ContentType, attachment.Size, ct);

                processingResult = "Created";
            }
            else
            {
                processingResult = "Skipped";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email {MessageId}", message.ExternalMessageId);
            processingResult = "Failed";

            var errorLog = new EmailProcessingLog
            {
                EmailConfigurationId = emailConfigurationId,
                ExternalMessageId = message.ExternalMessageId,
                Subject = message.Subject,
                SenderEmail = message.SenderEmail,
                ProcessingResult = processingResult,
                TicketId = ticketId,
                ErrorMessage = ex.Message,
                ProcessedAt = DateTimeOffset.UtcNow,
            };
            _context.EmailProcessingLogs.Add(errorLog);
            await _context.SaveChangesAsync(ct);

            return Result<Guid?>.Failure(ex.Message);
        }

        var log = new EmailProcessingLog
        {
            EmailConfigurationId = emailConfigurationId,
            ExternalMessageId = message.ExternalMessageId,
            Subject = message.Subject,
            SenderEmail = message.SenderEmail,
            ProcessingResult = processingResult,
            TicketId = ticketId,
            ProcessedAt = DateTimeOffset.UtcNow,
        };
        _context.EmailProcessingLogs.Add(log);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Email {MessageId} processing result: {Result}", message.ExternalMessageId, processingResult);
        return Result<Guid?>.Success(ticketId);
    }
}
