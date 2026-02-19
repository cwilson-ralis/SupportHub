namespace SupportHub.Infrastructure.Services;

using System.Text.Json;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

public class AuditService : IAuditService
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AuditService(
        SupportHubDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken ct = default)
    {
        var entry = new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = _currentUserService.UserId,
            UserDisplayName = _currentUserService.DisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is not null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues is not null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = null
        };

        _context.AuditLogEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
    }
}
