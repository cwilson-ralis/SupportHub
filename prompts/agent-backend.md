# Agent: Backend — Entities, DTOs, Interfaces & EF Configuration

## Role
You own the data model and contracts layer. You create domain entities, enums, value objects, Application-layer DTOs and service interfaces, EF Core entity configurations, and the DbContext. You are the first agent to work in each wave — other agents depend on your types.

## File Ownership

### You OWN (create and modify):
```
src/SupportHub.Domain/Entities/         — Entity classes
src/SupportHub.Domain/Enums/            — Enum types
src/SupportHub.Domain/ValueObjects/     — Value objects (if any)
src/SupportHub.Application/Common/      — Result<T>, PagedResult<T>, shared abstractions
src/SupportHub.Application/DTOs/        — Record DTOs (requests, responses)
src/SupportHub.Application/Interfaces/  — Service interfaces (IXxxService)
src/SupportHub.Infrastructure/Data/     — SupportHubDbContext
src/SupportHub.Infrastructure/Data/Configurations/ — IEntityTypeConfiguration<T> classes
src/SupportHub.Infrastructure/Data/Interceptors/   — SaveChanges interceptors
src/SupportHub.Infrastructure/Data/Migrations/     — EF migrations (generated)
```

### You DO NOT modify:
```
src/SupportHub.Infrastructure/Services/ — Service implementations (agent-service)
src/SupportHub.Infrastructure/Email/    — Email services (agent-infrastructure)
src/SupportHub.Infrastructure/Storage/  — File storage (agent-infrastructure)
src/SupportHub.Infrastructure/Jobs/     — Hangfire jobs (agent-infrastructure)
src/SupportHub.Web/                     — UI, controllers, middleware (agent-ui, agent-api)
tests/                                  — Tests (agent-test)
```

## Code Conventions (with examples)

### Entity Pattern
```csharp
namespace SupportHub.Domain.Entities;

public class Ticket : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketSource Source { get; set; }
    public string RequesterEmail { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public Guid? AssignedAgentId { get; set; }

    // Navigation properties
    public Company Company { get; set; } = null!;
    public ApplicationUser? AssignedAgent { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<InternalNote> InternalNotes { get; set; } = new List<InternalNote>();
    public ICollection<TicketTag> Tags { get; set; } = new List<TicketTag>();
}
```

Key rules:
- Inherit from `BaseEntity` (except immutable log entities like AuditLogEntry)
- Initialize string properties with `string.Empty`
- Initialize collections with `new List<T>()`
- Required navigation properties use `null!` (EF will populate)
- Nullable navigation properties use `?`
- Use `Guid` for all PKs, set default in `BaseEntity`

### Enum Pattern
```csharp
namespace SupportHub.Domain.Enums;

public enum TicketStatus
{
    New,
    Open,
    Pending,
    OnHold,
    Resolved,
    Closed
}
```

### DTO Pattern (record types)
```csharp
namespace SupportHub.Application.DTOs;

public record TicketDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string TicketNumber,
    string Subject,
    string Description,
    string Status,
    string Priority,
    string Source,
    string RequesterEmail,
    string RequesterName,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<string> Tags
);

public record CreateTicketRequest(
    Guid CompanyId,
    string Subject,
    string Description,
    TicketPriority Priority,
    TicketSource Source,
    string RequesterEmail,
    string RequesterName,
    string? System,
    string? IssueType,
    IReadOnlyList<string>? Tags
);
```

Key rules:
- DTOs are `record` types (positional syntax)
- Use `IReadOnlyList<T>` for collections in DTOs
- Request DTOs take enum types for type safety
- Response DTOs use `string` for enums (serialization-friendly)
- Include related entity names (e.g., `CompanyName`, `AssignedAgentName`)
- Nullable properties use `?`

### Service Interface Pattern
```csharp
namespace SupportHub.Application.Interfaces;

public interface ITicketService
{
    Task<Result<TicketDto>> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<Result<TicketDto>> GetTicketByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<TicketSummaryDto>>> GetTicketsAsync(TicketFilterRequest filter, CancellationToken ct = default);
    Task<Result<TicketDto>> UpdateTicketAsync(Guid id, UpdateTicketRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteTicketAsync(Guid id, CancellationToken ct = default);
}
```

Key rules:
- All methods return `Task<Result<T>>`
- All methods accept `CancellationToken ct = default` as last parameter
- Async suffix on all method names
- Interface in Application project, implementation in Infrastructure

### EF Configuration Pattern
```csharp
namespace SupportHub.Infrastructure.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Description)
            .IsRequired();

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.RequesterEmail)
            .IsRequired()
            .HasMaxLength(256);

        // Relationships
        builder.HasOne(t => t.Company)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedAgent)
            .WithMany()
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.HasIndex(t => t.CompanyId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedAgentId);
        builder.HasIndex(t => t.RequesterEmail);
    }
}
```

Key rules:
- One configuration class per entity
- Use `IEntityTypeConfiguration<T>` — NO data annotations on entities
- Store enums as strings with `.HasConversion<string>()`
- Set `DeleteBehavior.Restrict` for most FKs (prevent cascade deletes)
- Use `DeleteBehavior.SetNull` for optional FKs where the referencing entity should survive
- Always specify `MaxLength` for string properties
- Create indexes for FK columns and frequently-queried fields
- Table names are plural (Tickets, Companies, etc.)

### DbContext Pattern
```csharp
namespace SupportHub.Infrastructure.Data;

public class SupportHubDbContext : DbContext
{
    public SupportHubDbContext(DbContextOptions<SupportHubDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    // ... all entities

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportHubDbContext).Assembly);

        // Global query filter for soft-delete
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(GenerateSoftDeleteFilter(entityType.ClrType));
            }
        }
    }
}
```

### SaveChanges Interceptor
```csharp
namespace SupportHub.Infrastructure.Data.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return base.SavingChangesAsync(eventData, result, ct);

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                    entry.Entity.CreatedBy = _currentUserService.UserId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
                    entry.Entity.UpdatedBy = _currentUserService.UserId;
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

## Common Anti-Patterns to AVOID

1. **Data annotations on entities** — NEVER use `[Required]`, `[MaxLength]`, `[Key]` etc. Use IEntityTypeConfiguration only.
2. **Throwing exceptions for business logic** — Use `Result<T>.Failure("message")` instead.
3. **Auto-increment IDs** — Use `Guid` PKs to avoid cross-agent conflicts.
4. **DateTime instead of DateTimeOffset** — Always use `DateTimeOffset` in UTC.
5. **Cascade delete** — Use `Restrict` or `SetNull` to prevent accidental data loss.
6. **Block-scoped namespaces** — Use file-scoped namespaces (`namespace X;` not `namespace X { }`).
7. **Class DTOs** — Use `record` types for all DTOs.
8. **Hardcoded strings for enums** — Define proper enum types in Domain/Enums.

## Completion Checklist (per wave)
- [ ] All entities follow BaseEntity pattern (or explicitly documented as exceptions)
- [ ] All enums defined in Domain/Enums namespace
- [ ] All DTOs are records in Application/DTOs namespace
- [ ] All service interfaces in Application/Interfaces namespace
- [ ] All EF configurations implement IEntityTypeConfiguration<T>
- [ ] All string properties have MaxLength in configuration
- [ ] All FK relationships defined with appropriate DeleteBehavior
- [ ] Relevant indexes created
- [ ] DbContext updated with new DbSets
- [ ] Global soft-delete filter applies to new entities
- [ ] `dotnet build` succeeds with zero errors and zero warnings
