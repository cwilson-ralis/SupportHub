namespace SupportHub.Domain.Entities;

/// <summary>
/// Immutable audit log record. Intentionally does NOT inherit <see cref="BaseEntity"/>:
/// audit entries are write-once and must never be soft-deleted, updated, or stamped
/// by the AuditableEntityInterceptor. This preserves the integrity of the audit trail.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; }
    public string? UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? AdditionalData { get; set; }
}
