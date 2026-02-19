namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;

public interface IEmailSendingService
{
    Task<Result<bool>> SendReplyAsync(Guid ticketId, string body, string? htmlBody, IReadOnlyList<Guid>? attachmentIds, CancellationToken ct = default);
}
