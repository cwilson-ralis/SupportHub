namespace SupportHub.Infrastructure.Jobs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportHub.Application.Interfaces;
using SupportHub.Infrastructure.Data;

public class EmailPollingJob(
    SupportHubDbContext _context,
    IEmailPollingService _emailPollingService,
    ILogger<EmailPollingJob> _logger)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var configs = await _context.EmailConfigurations
            .Where(c => c.IsActive && !c.IsDeleted)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var config in configs)
        {
            var due = config.LastPolledAt is null
                || DateTimeOffset.UtcNow - config.LastPolledAt.Value >= TimeSpan.FromMinutes(config.PollingIntervalMinutes);
            if (!due) continue;

            var result = await _emailPollingService.PollMailboxAsync(config.Id, ct);
            if (result.IsSuccess)
            {
                processed += result.Value;
                _logger.LogInformation("Polled {Count} messages for config {Id}", result.Value, config.Id);
            }
            else
            {
                _logger.LogError("Polling failed for config {Id}: {Error}", config.Id, result.Error);
            }
        }

        _logger.LogInformation("Email polling completed. Processed {Total} messages across {ConfigCount} configurations", processed, configs.Count);
    }
}
