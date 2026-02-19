namespace SupportHub.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;
using SupportHub.Infrastructure.Data;

public class EmailPollingService(
    SupportHubDbContext _context,
    IGraphClientFactory _graphClientFactory,
    IEmailProcessingService _emailProcessingService,
    ILogger<EmailPollingService> _logger) : IEmailPollingService
{
    public async Task<Result<int>> PollMailboxAsync(Guid emailConfigurationId, CancellationToken ct = default)
    {
        var config = await _context.EmailConfigurations
            .FirstOrDefaultAsync(c => c.Id == emailConfigurationId && !c.IsDeleted, ct);
        if (config is null)
            return Result<int>.Failure("Email configuration not found.");
        if (!config.IsActive)
            return Result<int>.Failure("Email configuration is inactive.");

        var graphClient = _graphClientFactory.CreateClient();
        var processedCount = 0;

        try
        {
            var messagesPage = await graphClient.Users[config.SharedMailboxAddress].Messages
                .GetAsync(req =>
                {
                    req.QueryParameters.Select = ["id", "subject", "from", "body", "receivedDateTime", "internetMessageHeaders", "hasAttachments"];
                    req.QueryParameters.Top = 50;
                    if (config.LastPolledAt.HasValue)
                        req.QueryParameters.Filter = $"receivedDateTime gt {config.LastPolledAt.Value.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}";
                }, ct);

            var messages = messagesPage?.Value ?? [];

            foreach (var graphMessage in messages)
            {
                if (graphMessage?.Id is null) continue;

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (graphMessage.InternetMessageHeaders is not null)
                    foreach (var h in graphMessage.InternetMessageHeaders)
                        if (h.Name is not null && h.Value is not null)
                            headers[h.Name] = h.Value;

                var attachments = new List<EmailAttachment>();

                // Download attachments if present
                if (graphMessage.HasAttachments == true)
                {
                    var graphAttachments = await graphClient.Users[config.SharedMailboxAddress]
                        .Messages[graphMessage.Id].Attachments
                        .GetAsync(cancellationToken: ct);

                    if (graphAttachments?.Value is not null)
                    {
                        foreach (var att in graphAttachments.Value)
                        {
                            if (att is Microsoft.Graph.Models.FileAttachment fileAtt && fileAtt.ContentBytes is not null)
                            {
                                attachments.Add(new EmailAttachment(
                                    fileAtt.Name ?? "attachment",
                                    fileAtt.ContentType ?? "application/octet-stream",
                                    fileAtt.Size ?? fileAtt.ContentBytes.Length,
                                    new MemoryStream(fileAtt.ContentBytes)));
                            }
                        }
                    }
                }

                var inbound = new InboundEmailMessage(
                    ExternalMessageId: graphMessage.Id,
                    Subject: graphMessage.Subject ?? string.Empty,
                    Body: graphMessage.Body?.Content ?? string.Empty,
                    HtmlBody: graphMessage.Body?.ContentType == Microsoft.Graph.Models.BodyType.Html ? graphMessage.Body?.Content : null,
                    SenderEmail: graphMessage.From?.EmailAddress?.Address ?? string.Empty,
                    SenderName: graphMessage.From?.EmailAddress?.Name ?? string.Empty,
                    ReceivedAt: graphMessage.ReceivedDateTime ?? DateTimeOffset.UtcNow,
                    Attachments: attachments,
                    InternetHeaders: headers);

                var result = await _emailProcessingService.ProcessInboundEmailAsync(inbound, emailConfigurationId, ct);
                if (result.IsSuccess && result.Value.HasValue)
                    processedCount++;
            }

            config.LastPolledAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Polled mailbox {Mailbox}: processed {Count} messages", config.SharedMailboxAddress, processedCount);
            return Result<int>.Success(processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll mailbox {Mailbox}", config.SharedMailboxAddress);
            return Result<int>.Failure($"Polling failed: {ex.Message}");
        }
    }
}
