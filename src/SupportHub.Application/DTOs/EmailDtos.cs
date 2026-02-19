namespace SupportHub.Application.DTOs;

public record InboundEmailMessage(
    string ExternalMessageId,
    string Subject,
    string Body,
    string? HtmlBody,
    string SenderEmail,
    string SenderName,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<EmailAttachment> Attachments,
    IReadOnlyDictionary<string, string> InternetHeaders);

public record EmailAttachment(
    string FileName,
    string ContentType,
    long Size,
    Stream Content);
