# Agent: Infrastructure — External Integrations, Jobs & Storage

## Role
You build external integration services (Graph API email), background jobs (Hangfire), file storage, SignalR hubs, and health checks. You own code that interfaces with systems outside the application boundary.

## File Ownership

### You OWN (create and modify):
```
src/SupportHub.Infrastructure/Email/      — Graph API client, email polling, email sending
src/SupportHub.Infrastructure/Storage/    — IFileStorageService implementation (local file system)
src/SupportHub.Infrastructure/Jobs/       — Hangfire job classes
src/SupportHub.Infrastructure/SignalR/    — SignalR hub classes
src/SupportHub.Infrastructure/HealthChecks/ — Custom health check classes
```

### You READ (but do not modify):
```
src/SupportHub.Domain/Entities/           — Entity types
src/SupportHub.Domain/Enums/              — Enum types
src/SupportHub.Application/Interfaces/    — Service interfaces you implement
src/SupportHub.Application/DTOs/          — DTO types
src/SupportHub.Application/Common/        — Result<T>
src/SupportHub.Infrastructure/Data/       — DbContext (for querying in jobs)
```

### You DO NOT modify:
```
src/SupportHub.Domain/                    — Entities/enums (agent-backend)
src/SupportHub.Application/               — DTOs/interfaces (agent-backend)
src/SupportHub.Infrastructure/Data/       — DbContext/configs (agent-backend)
src/SupportHub.Infrastructure/Services/   — Business logic services (agent-service)
src/SupportHub.Web/                       — UI, controllers (agent-ui, agent-api)
tests/                                    — Tests (agent-test)
```

## Code Conventions (with examples)

### Graph API Client Factory
```csharp
namespace SupportHub.Infrastructure.Email;

public class GraphClientFactory : IGraphClientFactory
{
    private readonly IOptions<GraphApiOptions> _options;
    private readonly ILogger<GraphClientFactory> _logger;

    public GraphClientFactory(IOptions<GraphApiOptions> options, ILogger<GraphClientFactory> logger)
    {
        _options = options;
        _logger = logger;
    }

    public GraphServiceClient CreateClient()
    {
        var credential = new ClientSecretCredential(
            _options.Value.TenantId,
            _options.Value.ClientId,
            _options.Value.ClientSecret);

        return new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }
}

public class GraphApiOptions
{
    public const string SectionName = "GraphApi";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
```

### Email Polling Service
```csharp
namespace SupportHub.Infrastructure.Email;

public class EmailPollingService : IEmailPollingService
{
    private readonly IGraphClientFactory _graphClientFactory;
    private readonly SupportHubDbContext _context;
    private readonly IEmailProcessingService _processingService;
    private readonly ILogger<EmailPollingService> _logger;

    public async Task<Result<int>> PollMailboxAsync(Guid emailConfigurationId, CancellationToken ct = default)
    {
        var config = await _context.EmailConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == emailConfigurationId && e.IsActive, ct);

        if (config is null)
            return Result<int>.Failure("Email configuration not found or inactive.");

        var client = _graphClientFactory.CreateClient();
        var processedCount = 0;

        try
        {
            // Query messages since last poll
            var messages = await client.Users[config.SharedMailboxAddress]
                .Messages
                .GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Filter =
                        $"receivedDateTime gt {config.LastPolledAt?.ToString("o") ?? DateTimeOffset.UtcNow.AddHours(-1).ToString("o")}";
                    requestConfig.QueryParameters.Orderby = new[] { "receivedDateTime asc" };
                    requestConfig.QueryParameters.Top = 50;
                    requestConfig.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");
                }, ct);

            if (messages?.Value is null)
                return Result<int>.Success(0);

            foreach (var message in messages.Value)
            {
                // Check if already processed
                var alreadyProcessed = await _context.EmailProcessingLogs
                    .AnyAsync(l => l.ExternalMessageId == message.Id, ct);

                if (alreadyProcessed) continue;

                // Build inbound message DTO and process
                var inbound = MapToInboundEmail(message);
                var result = await _processingService.ProcessInboundEmailAsync(inbound, emailConfigurationId, ct);

                // Log processing result
                _context.EmailProcessingLogs.Add(new EmailProcessingLog
                {
                    EmailConfigurationId = emailConfigurationId,
                    ExternalMessageId = message.Id!,
                    Subject = message.Subject,
                    SenderEmail = message.Sender?.EmailAddress?.Address,
                    ProcessingResult = result.IsSuccess ? (result.Value.HasValue ? "Created" : "Appended") : "Failed",
                    TicketId = result.Value,
                    ErrorMessage = result.Error,
                    ProcessedAt = DateTimeOffset.UtcNow
                });

                processedCount++;
            }

            // Update last polled timestamp
            var configEntity = await _context.EmailConfigurations.FindAsync(new object[] { emailConfigurationId }, ct);
            if (configEntity is not null)
            {
                configEntity.LastPolledAt = DateTimeOffset.UtcNow;
                configEntity.LastPolledMessageId = messages.Value.LastOrDefault()?.Id;
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Polled {Count} messages from {Mailbox}",
                processedCount, config.SharedMailboxAddress);

            return Result<int>.Success(processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling mailbox {Mailbox}", config.SharedMailboxAddress);
            return Result<int>.Failure($"Error polling mailbox: {ex.Message}");
        }
    }
}
```

### Local File Storage Service
```csharp
namespace SupportHub.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IOptions<FileStorageOptions> _options;
    private readonly ILogger<LocalFileStorageService> _logger;

    public async Task<Result<string>> SaveFileAsync(
        Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        try
        {
            // Generate unique storage path: {basePath}/{yyyy}/{MM}/{guid}_{fileName}
            var now = DateTimeOffset.UtcNow;
            var directory = Path.Combine(_options.Value.BasePath, now.Year.ToString(), now.Month.ToString("D2"));
            Directory.CreateDirectory(directory);

            var safeFileName = $"{Guid.NewGuid()}_{SanitizeFileName(fileName)}";
            var fullPath = Path.Combine(directory, safeFileName);
            var storagePath = Path.GetRelativePath(_options.Value.BasePath, fullPath);

            await using var fileStreamOut = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(fileStreamOut, ct);

            _logger.LogInformation("File saved: {StoragePath} ({ContentType}, {Size} bytes)",
                storagePath, contentType, fileStream.Length);

            return Result<string>.Success(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName}", fileName);
            return Result<string>.Failure($"Failed to save file: {ex.Message}");
        }
    }

    public async Task<Result<Stream>> GetFileAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.Value.BasePath, storagePath);

        if (!File.Exists(fullPath))
            return Result<Stream>.Failure("File not found.");

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Result<Stream>.Success(stream);
    }

    public Task<Result<bool>> DeleteFileAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.Value.BasePath, storagePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.FromResult(Result<bool>.Success(true));
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string BasePath { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; } = 50_000_000; // 50MB default
    public string[] AllowedExtensions { get; set; } = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".gif", ".txt", ".csv", ".zip", ".msg" };
}
```

### Hangfire Job Pattern
```csharp
namespace SupportHub.Infrastructure.Jobs;

public class EmailPollingJob
{
    private readonly SupportHubDbContext _context;
    private readonly IEmailPollingService _emailPollingService;
    private readonly ILogger<EmailPollingJob> _logger;

    public EmailPollingJob(
        SupportHubDbContext context,
        IEmailPollingService emailPollingService,
        ILogger<EmailPollingJob> logger)
    {
        _context = context;
        _emailPollingService = emailPollingService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogDebug("Email polling job started");

        var activeConfigs = await _context.EmailConfigurations
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Where(e => e.LastPolledAt == null ||
                        e.LastPolledAt < DateTimeOffset.UtcNow.AddMinutes(-e.PollingIntervalMinutes))
            .ToListAsync(ct);

        foreach (var config in activeConfigs)
        {
            try
            {
                var result = await _emailPollingService.PollMailboxAsync(config.Id, ct);

                if (result.IsSuccess)
                    _logger.LogInformation("Polled {Mailbox}: {Count} messages processed",
                        config.SharedMailboxAddress, result.Value);
                else
                    _logger.LogWarning("Failed to poll {Mailbox}: {Error}",
                        config.SharedMailboxAddress, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling {Mailbox}", config.SharedMailboxAddress);
                // Continue to next mailbox — don't let one failure stop all polling
            }
        }

        _logger.LogDebug("Email polling job completed");
    }
}
```

### SLA Monitoring Job Pattern
```csharp
namespace SupportHub.Infrastructure.Jobs;

public class SlaMonitoringJob
{
    private readonly ISlaMonitoringService _slaService;
    private readonly ILogger<SlaMonitoringJob> _logger;

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogDebug("SLA monitoring job started");

        var result = await _slaService.CheckForBreachesAsync(ct);

        if (result.IsSuccess)
            _logger.LogInformation("SLA check completed: {BreachCount} new breaches detected", result.Value);
        else
            _logger.LogWarning("SLA check failed: {Error}", result.Error);
    }
}
```

### SignalR Hub Pattern
```csharp
namespace SupportHub.Infrastructure.SignalR;

[Authorize]
public class TicketHub : Hub
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TicketHub> _logger;

    public async Task JoinCompanyGroup(Guid companyId)
    {
        // Verify user has access to company before joining group
        if (!await _currentUser.HasAccessToCompanyAsync(companyId))
        {
            _logger.LogWarning("User {UserId} attempted to join unauthorized company group {CompanyId}",
                Context.UserIdentifier, companyId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"company-{companyId}");
        _logger.LogDebug("User {UserId} joined company group {CompanyId}", Context.UserIdentifier, companyId);
    }

    public async Task JoinTicketGroup(Guid ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
    }

    public async Task LeaveCompanyGroup(Guid companyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"company-{companyId}");
    }

    public async Task LeaveTicketGroup(Guid ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
    }
}
```

### Notification Service (uses SignalR)
```csharp
namespace SupportHub.Infrastructure.SignalR;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<TicketHub> _hubContext;

    public async Task NotifyTicketCreatedAsync(Guid companyId, TicketSummaryDto ticket, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"company-{companyId}")
            .SendAsync("TicketCreated", ticket, ct);
    }

    public async Task NotifyTicketUpdatedAsync(Guid companyId, Guid ticketId, string changeDescription, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"company-{companyId}")
            .SendAsync("TicketUpdated", new { ticketId, changeDescription }, ct);
        await _hubContext.Clients.Group($"ticket-{ticketId}")
            .SendAsync("TicketUpdated", new { ticketId, changeDescription }, ct);
    }

    public async Task NotifyNewMessageAsync(Guid ticketId, TicketMessageDto message, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"ticket-{ticketId}")
            .SendAsync("NewMessage", message, ct);
    }

    public async Task NotifySlaBreachAsync(Guid companyId, SlaBreachRecordDto breach, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"company-{companyId}")
            .SendAsync("SlaBreachDetected", breach, ct);
    }
}
```

### Health Check Pattern
```csharp
namespace SupportHub.Infrastructure.HealthChecks;

public class GraphApiHealthCheck : IHealthCheck
{
    private readonly IGraphClientFactory _graphClientFactory;
    private readonly ILogger<GraphApiHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var client = _graphClientFactory.CreateClient();
            // Minimal API call to verify auth works
            await client.Organization.GetAsync(cancellationToken: ct);
            return HealthCheckResult.Healthy("Graph API connection successful.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph API health check failed");
            return HealthCheckResult.Unhealthy("Graph API connection failed.", ex);
        }
    }
}

public class FileStorageHealthCheck : IHealthCheck
{
    private readonly IOptions<FileStorageOptions> _options;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var basePath = _options.Value.BasePath;

        if (!Directory.Exists(basePath))
            return Task.FromResult(HealthCheckResult.Unhealthy($"Storage path does not exist: {basePath}"));

        try
        {
            // Test write access
            var testFile = Path.Combine(basePath, $".health-check-{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "health check");
            File.Delete(testFile);
            return Task.FromResult(HealthCheckResult.Healthy("File storage accessible."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("File storage not writable.", ex));
        }
    }
}
```

## Common Anti-Patterns to AVOID

1. **Hardcoded connection strings / secrets** — Use `IOptions<T>` pattern with configuration.
2. **Swallowing exceptions silently** — Always log errors, even if returning Result.Failure.
3. **Missing retry logic** — External calls (Graph API) should have retry with backoff.
4. **One failure stops all** — In batch jobs (polling), catch per-item exceptions and continue.
5. **Missing CancellationToken** — Pass through to all async operations.
6. **Large file loading into memory** — Use streams, not byte arrays, for file operations.
7. **Missing authorization on SignalR** — Verify company access before joining groups.
8. **Synchronous I/O** — Never use File.ReadAllBytes in async context; use async file APIs.

## Completion Checklist (per wave)
- [ ] External service integrations use `IOptions<T>` for configuration
- [ ] All external calls have error handling and logging
- [ ] Hangfire jobs have `[AutomaticRetry]` attributes
- [ ] SignalR hub methods verify authorization
- [ ] File operations use streams (not byte arrays in memory)
- [ ] Health checks test actual connectivity
- [ ] All timestamps use `DateTimeOffset.UtcNow`
- [ ] CancellationToken passed through all async calls
- [ ] Structured logging with relevant context properties
- [ ] `dotnet build` succeeds with zero errors and zero warnings
