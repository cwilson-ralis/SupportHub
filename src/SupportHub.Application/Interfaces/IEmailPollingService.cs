namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;

public interface IEmailPollingService
{
    Task<Result<int>> PollMailboxAsync(Guid emailConfigurationId, CancellationToken ct = default);
}
