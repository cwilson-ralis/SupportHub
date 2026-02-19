# Phase 2 — Core Ticketing

## Overview

Web form intake, ticket lifecycle, attachments, internal notes, canned responses, tags. This phase delivers the primary ticket management experience — the central feature surface of Ralis Support Hub.

## Prerequisites

- Phase 1 complete (BaseEntity, Company, ApplicationUser, DbContext, Azure AD auth, shell layout)
- `Result<T>` and `PagedResult<T>` types available in SupportHub.Application
- EF Core migrations baseline applied

---

## Wave 1 — Domain Entities & Enums

All entities live in **SupportHub.Domain**. Every entity inherits from `BaseEntity` (Id `Guid`, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, IsDeleted, DeletedAt). All timestamps are `DateTimeOffset` in UTC.

### Enums

File: `src/SupportHub.Domain/Enums/TicketStatus.cs`

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

File: `src/SupportHub.Domain/Enums/TicketPriority.cs`

```csharp
namespace SupportHub.Domain.Enums;

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}
```

File: `src/SupportHub.Domain/Enums/TicketSource.cs`

```csharp
namespace SupportHub.Domain.Enums;

public enum TicketSource
{
    WebForm,
    Email,
    Api,
    Internal
}
```

File: `src/SupportHub.Domain/Enums/MessageDirection.cs`

```csharp
namespace SupportHub.Domain.Enums;

public enum MessageDirection
{
    Inbound,
    Outbound
}
```

### Ticket Entity

File: `src/SupportHub.Domain/Entities/Ticket.cs`

```csharp
namespace SupportHub.Domain.Entities;

public class Ticket : BaseEntity
{
    // Identity
    public Guid CompanyId { get; set; }                   // FK → Company, required
    public Guid? QueueId { get; set; }                    // FK → Queue (Phase 4), nullable
    public string TicketNumber { get; set; } = string.Empty; // Auto-generated, unique, "TKT-{YYYYMMDD}-{sequential}"

    // Core fields
    public string Subject { get; set; } = string.Empty;   // Required, max 500
    public string Description { get; set; } = string.Empty; // Required
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketSource Source { get; set; }

    // Requester
    public string RequesterEmail { get; set; } = string.Empty; // Required, max 256
    public string RequesterName { get; set; } = string.Empty;  // Required, max 200

    // Assignment
    public Guid? AssignedAgentId { get; set; }            // FK → ApplicationUser, nullable

    // Categorization
    public string? System { get; set; }                   // Max 200 — application/system the ticket relates to
    public string? IssueType { get; set; }                // Max 200

    // Lifecycle timestamps
    public DateTimeOffset? FirstResponseAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    // AI
    public string? AiClassification { get; set; }         // JSON blob for AI routing metadata

    // Navigation properties
    public Company Company { get; set; } = null!;
    public ApplicationUser? AssignedAgent { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<InternalNote> InternalNotes { get; set; } = new List<InternalNote>();
    public ICollection<TicketTag> Tags { get; set; } = new List<TicketTag>();
}
```

### TicketMessage Entity

File: `src/SupportHub.Domain/Entities/TicketMessage.cs`

```csharp
namespace SupportHub.Domain.Entities;

public class TicketMessage : BaseEntity
{
    public Guid TicketId { get; set; }                    // FK → Ticket, required
    public MessageDirection Direction { get; set; }
    public string? SenderEmail { get; set; }              // Max 256
    public string? SenderName { get; set; }               // Max 200
    public string Body { get; set; } = string.Empty;      // Required (plain text)
    public string? HtmlBody { get; set; }                 // Rich HTML version
    public string? ExternalMessageId { get; set; }        // Max 500 — Graph message ID for email threading

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}
```

### TicketAttachment Entity

File: `src/SupportHub.Domain/Entities/TicketAttachment.cs`

```csharp
namespace SupportHub.Domain.Entities;

public class TicketAttachment : BaseEntity
{
    public Guid TicketId { get; set; }                    // FK → Ticket, required
    public Guid? TicketMessageId { get; set; }            // FK → TicketMessage, nullable (null = attached directly to ticket)
    public string FileName { get; set; } = string.Empty;  // Required, max 500 — stored file name (may be sanitized/uniquified)
    public string OriginalFileName { get; set; } = string.Empty; // Required, max 500 — user's original file name
    public string ContentType { get; set; } = string.Empty; // Required, max 200
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty; // Required, max 1000 — path on network share

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
    public TicketMessage? TicketMessage { get; set; }
}
```

### InternalNote Entity

File: `src/SupportHub.Domain/Entities/InternalNote.cs`

```csharp
namespace SupportHub.Domain.Entities;

public class InternalNote : BaseEntity
{
    public Guid TicketId { get; set; }                    // FK → Ticket, required
    public Guid AuthorId { get; set; }                    // FK → ApplicationUser, required
    public string Body { get; set; } = string.Empty;      // Required

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
}
```

### TicketTag Entity

File: `src/SupportHub.Domain/Entities/TicketTag.cs`

```csharp
namespace SupportHub.Domain.Entities;

public class TicketTag : BaseEntity
{
    public Guid TicketId { get; set; }                    // FK → Ticket, required
    public string Tag { get; set; } = string.Empty;       // Required, max 100

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;
}
```

**Unique constraint:** `TicketId` + `Tag` (case-insensitive).

### CannedResponse Entity

File: `src/SupportHub.Domain/Entities/CannedResponse.cs`

```csharp
namespace SupportHub.Domain.Entities;

public class CannedResponse : BaseEntity
{
    public Guid? CompanyId { get; set; }                  // FK → Company, nullable (null = global)
    public string Title { get; set; } = string.Empty;     // Required, max 200
    public string Body { get; set; } = string.Empty;      // Required
    public string? Category { get; set; }                 // Max 100
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Company? Company { get; set; }
}
```

---

## Wave 2 — EF Configurations & Migration

All configurations use `IEntityTypeConfiguration<T>` in **SupportHub.Infrastructure**. No data annotations on entity classes.

### TicketConfiguration

File: `src/SupportHub.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(t => t.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Description)
            .IsRequired();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.RequesterEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.RequesterName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.System).HasMaxLength(200);
        builder.Property(t => t.IssueType).HasMaxLength(200);

        // Indexes
        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.HasIndex(t => t.CompanyId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedAgentId);
        builder.HasIndex(t => t.RequesterEmail);
        builder.HasIndex(t => new { t.CompanyId, t.Status });

        // Relationships
        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedAgent)
            .WithMany()
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Messages)
            .WithOne(m => m.Ticket)
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Attachments)
            .WithOne(a => a.Ticket)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.InternalNotes)
            .WithOne(n => n.Ticket)
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Tags)
            .WithOne(tag => tag.Ticket)
            .HasForeignKey(tag => tag.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global query filter for soft-delete
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
```

### TicketMessageConfiguration

File: `src/SupportHub.Infrastructure/Persistence/Configurations/TicketMessageConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Persistence.Configurations;

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Direction)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.SenderEmail).HasMaxLength(256);
        builder.Property(m => m.SenderName).HasMaxLength(200);
        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.ExternalMessageId).HasMaxLength(500);

        // Indexes
        builder.HasIndex(m => m.TicketId);
        builder.HasIndex(m => m.ExternalMessageId);

        // Relationships
        builder.HasMany(m => m.Attachments)
            .WithOne(a => a.TicketMessage)
            .HasForeignKey(a => a.TicketMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
```

### TicketAttachmentConfiguration

File: `src/SupportHub.Infrastructure/Persistence/Configurations/TicketAttachmentConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Persistence.Configurations;

public class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(1000);

        // Indexes
        builder.HasIndex(a => a.TicketId);
        builder.HasIndex(a => a.TicketMessageId);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
```

### InternalNoteConfiguration

File: `src/SupportHub.Infrastructure/Persistence/Configurations/InternalNoteConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Persistence.Configurations;

public class InternalNoteConfiguration : IEntityTypeConfiguration<InternalNote>
{
    public void Configure(EntityTypeBuilder<InternalNote> builder)
    {
        builder.ToTable("InternalNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Body).IsRequired();

        // Indexes
        builder.HasIndex(n => n.TicketId);

        // Relationships
        builder.HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(n => !n.IsDeleted);
    }
}
```

### TicketTagConfiguration

File: `src/SupportHub.Infrastructure/Persistence/Configurations/TicketTagConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Persistence.Configurations;

public class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.ToTable("TicketTags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Tag).IsRequired().HasMaxLength(100);

        // Unique constraint: one tag per ticket (case-insensitive enforced at service layer)
        builder.HasIndex(t => new { t.TicketId, t.Tag }).IsUnique();

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
```

### CannedResponseConfiguration

File: `src/SupportHub.Infrastructure/Persistence/Configurations/CannedResponseConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Persistence.Configurations;

public class CannedResponseConfiguration : IEntityTypeConfiguration<CannedResponse>
{
    public void Configure(EntityTypeBuilder<CannedResponse> builder)
    {
        builder.ToTable("CannedResponses");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Body).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(100);

        // Indexes
        builder.HasIndex(c => c.CompanyId);
        builder.HasIndex(c => c.Category);

        // Relationships
        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
```

### DbContext Registration

Add `DbSet<T>` properties to `SupportHubDbContext`:

```csharp
public DbSet<Ticket> Tickets => Set<Ticket>();
public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
public DbSet<InternalNote> InternalNotes => Set<InternalNote>();
public DbSet<TicketTag> TicketTags => Set<TicketTag>();
public DbSet<CannedResponse> CannedResponses => Set<CannedResponse>();
```

### Migration

```bash
dotnet ef migrations add AddCoreTicketing --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
dotnet ef database update --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
```

Review the generated migration to verify:
- All tables created with correct column types and constraints
- Indexes present on expected columns
- Foreign keys use `ON DELETE RESTRICT`
- `TicketNumber` unique index created
- `TicketTag` composite unique index on `(TicketId, Tag)` created

---

## Wave 3 — Service Interfaces & DTOs

All DTOs are `record` types in **SupportHub.Application/DTOs**. All service interfaces in **SupportHub.Application/Interfaces**.

### DTOs

File: `src/SupportHub.Application/DTOs/TicketDtos.cs`

```csharp
namespace SupportHub.Application.DTOs;

public record TicketDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string TicketNumber,
    string Subject,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    TicketSource Source,
    string RequesterEmail,
    string RequesterName,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    string? System,
    string? IssueType,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    string? AiClassification,
    IReadOnlyList<TicketTagDto> Tags,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public record TicketSummaryDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    TicketStatus Status,
    TicketPriority Priority,
    TicketSource Source,
    string RequesterName,
    string RequesterEmail,
    string CompanyName,
    string? AssignedAgentName,
    string? System,
    string? IssueType,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

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
    IReadOnlyList<string>? Tags);

public record UpdateTicketRequest(
    string? Subject,
    string? Description,
    TicketPriority? Priority,
    string? System,
    string? IssueType);

public record TicketFilterRequest(
    Guid? CompanyId,
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? AssignedAgentId,
    string? SearchTerm,
    IReadOnlyList<string>? Tags,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int Page = 1,
    int PageSize = 25);
```

File: `src/SupportHub.Application/DTOs/TicketMessageDtos.cs`

```csharp
namespace SupportHub.Application.DTOs;

public record TicketMessageDto(
    Guid Id,
    Guid TicketId,
    MessageDirection Direction,
    string? SenderEmail,
    string? SenderName,
    string Body,
    string? HtmlBody,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    DateTimeOffset CreatedAt);

public record CreateTicketMessageRequest(
    MessageDirection Direction,
    string? SenderEmail,
    string? SenderName,
    string Body,
    string? HtmlBody);
```

File: `src/SupportHub.Application/DTOs/TicketAttachmentDto.cs`

```csharp
namespace SupportHub.Application.DTOs;

public record TicketAttachmentDto(
    Guid Id,
    Guid TicketId,
    Guid? TicketMessageId,
    string FileName,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTimeOffset CreatedAt);
```

File: `src/SupportHub.Application/DTOs/InternalNoteDtos.cs`

```csharp
namespace SupportHub.Application.DTOs;

public record InternalNoteDto(
    Guid Id,
    Guid TicketId,
    Guid AuthorId,
    string AuthorName,
    string Body,
    DateTimeOffset CreatedAt);

public record CreateInternalNoteRequest(
    string Body);
```

File: `src/SupportHub.Application/DTOs/TicketTagDto.cs`

```csharp
namespace SupportHub.Application.DTOs;

public record TicketTagDto(
    Guid Id,
    Guid TicketId,
    string Tag);
```

File: `src/SupportHub.Application/DTOs/CannedResponseDtos.cs`

```csharp
namespace SupportHub.Application.DTOs;

public record CannedResponseDto(
    Guid Id,
    Guid? CompanyId,
    string? CompanyName,
    string Title,
    string Body,
    string? Category,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public record CreateCannedResponseRequest(
    Guid? CompanyId,
    string Title,
    string Body,
    string? Category,
    int SortOrder);

public record UpdateCannedResponseRequest(
    string? Title,
    string? Body,
    string? Category,
    int? SortOrder,
    bool? IsActive);
```

### Service Interfaces

File: `src/SupportHub.Application/Interfaces/ITicketService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface ITicketService
{
    Task<Result<TicketDto>> CreateTicketAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<Result<TicketDto>> GetTicketByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<TicketSummaryDto>>> GetTicketsAsync(TicketFilterRequest filter, CancellationToken ct = default);
    Task<Result<TicketDto>> UpdateTicketAsync(Guid id, UpdateTicketRequest request, CancellationToken ct = default);
    Task<Result<bool>> AssignTicketAsync(Guid ticketId, Guid agentId, CancellationToken ct = default);
    Task<Result<bool>> ChangeStatusAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct = default);
    Task<Result<bool>> ChangePriorityAsync(Guid ticketId, TicketPriority newPriority, CancellationToken ct = default);
    Task<Result<bool>> DeleteTicketAsync(Guid id, CancellationToken ct = default);
}
```

File: `src/SupportHub.Application/Interfaces/ITicketMessageService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface ITicketMessageService
{
    Task<Result<TicketMessageDto>> AddMessageAsync(Guid ticketId, CreateTicketMessageRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TicketMessageDto>>> GetMessagesAsync(Guid ticketId, CancellationToken ct = default);
}
```

File: `src/SupportHub.Application/Interfaces/IInternalNoteService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface IInternalNoteService
{
    Task<Result<InternalNoteDto>> AddNoteAsync(Guid ticketId, CreateInternalNoteRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<InternalNoteDto>>> GetNotesAsync(Guid ticketId, CancellationToken ct = default);
}
```

File: `src/SupportHub.Application/Interfaces/IFileStorageService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface IFileStorageService
{
    Task<Result<string>> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<Result<Stream>> GetFileAsync(string storagePath, CancellationToken ct = default);
    Task<Result<bool>> DeleteFileAsync(string storagePath, CancellationToken ct = default);
}
```

File: `src/SupportHub.Application/Interfaces/IAttachmentService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface IAttachmentService
{
    Task<Result<TicketAttachmentDto>> UploadAttachmentAsync(Guid ticketId, Guid? messageId, Stream fileStream, string fileName, string contentType, long fileSize, CancellationToken ct = default);
    Task<Result<(Stream FileStream, string ContentType, string FileName)>> DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TicketAttachmentDto>>> GetAttachmentsAsync(Guid ticketId, CancellationToken ct = default);
}
```

File: `src/SupportHub.Application/Interfaces/ICannedResponseService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface ICannedResponseService
{
    Task<Result<PagedResult<CannedResponseDto>>> GetCannedResponsesAsync(Guid? companyId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<CannedResponseDto>> CreateCannedResponseAsync(CreateCannedResponseRequest request, CancellationToken ct = default);
    Task<Result<CannedResponseDto>> UpdateCannedResponseAsync(Guid id, UpdateCannedResponseRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteCannedResponseAsync(Guid id, CancellationToken ct = default);
}
```

File: `src/SupportHub.Application/Interfaces/ITagService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface ITagService
{
    Task<Result<TicketTagDto>> AddTagAsync(Guid ticketId, string tag, CancellationToken ct = default);
    Task<Result<bool>> RemoveTagAsync(Guid ticketId, string tag, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> GetPopularTagsAsync(Guid? companyId, int count = 20, CancellationToken ct = default);
}
```

---

## Wave 4 — Service Implementations

All implementations in **SupportHub.Infrastructure/Services**. Each service receives `SupportHubDbContext`, `ILogger<T>`, and any other required dependencies via constructor injection.

### TicketService

File: `src/SupportHub.Infrastructure/Services/TicketService.cs`

Key behaviors:

1. **Ticket Number Generation** — On `CreateTicketAsync`, generate `TicketNumber` in format `TKT-YYYYMMDD-NNNN` where `NNNN` is a zero-padded sequential counter per day. Use a database-level approach to avoid race conditions:
   ```csharp
   // Query today's max ticket number, increment, retry on concurrency conflict
   var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
   var prefix = $"TKT-{today}-";
   var lastTicketNumber = await _dbContext.Tickets
       .Where(t => t.TicketNumber.StartsWith(prefix))
       .OrderByDescending(t => t.TicketNumber)
       .Select(t => t.TicketNumber)
       .FirstOrDefaultAsync(ct);

   var nextSequence = lastTicketNumber is null
       ? 1
       : int.Parse(lastTicketNumber[^4..]) + 1;

   ticket.TicketNumber = $"{prefix}{nextSequence:D4}";
   ```

2. **Company Isolation** — All queries filter by the current user's accessible company IDs. The service receives the current user context (via `ICurrentUserService` or similar) and restricts results accordingly. Never return tickets from companies the user cannot access.

3. **Status Transition Validation** — Enforce valid transitions:
   - `New` -> `Open`, `Pending`, `Closed`
   - `Open` -> `Pending`, `OnHold`, `Resolved`, `Closed`
   - `Pending` -> `Open`, `OnHold`, `Resolved`, `Closed`
   - `OnHold` -> `Open`, `Pending`, `Resolved`, `Closed`
   - `Resolved` -> `Open`, `Closed`
   - `Closed` -> `Open` (reopen)

   Return `Result.Failure` with descriptive error for invalid transitions.

4. **Auto-set Lifecycle Timestamps**:
   - `FirstResponseAt` — Set on first outbound message (via `ITicketMessageService` callback or domain event). Not set by status change alone.
   - `ResolvedAt` — Set when status changes to `Resolved`. Cleared if reopened (`Open`).
   - `ClosedAt` — Set when status changes to `Closed`. Cleared if reopened.

5. **Soft-Delete** — `DeleteTicketAsync` sets `IsDeleted = true` and `DeletedAt = DateTimeOffset.UtcNow`. Does not physically remove the row.

6. **Filtering and Pagination** — `GetTicketsAsync` builds an `IQueryable` dynamically from `TicketFilterRequest` fields. Apply `SearchTerm` against `Subject`, `RequesterName`, `RequesterEmail`, and `TicketNumber` using `Contains`. Apply tag filtering via a subquery join. Return `PagedResult<TicketSummaryDto>` with total count.

### TicketMessageService

File: `src/SupportHub.Infrastructure/Services/TicketMessageService.cs`

Key behaviors:

- Validates that the ticket exists and is accessible.
- Appends the message to the ticket's `Messages` collection.
- On the first **outbound** message, sets `Ticket.FirstResponseAt` if it is still `null`.
- If ticket status is `New` and an outbound message is added, automatically transition status to `Open`.
- Returns messages ordered by `CreatedAt` ascending.

### InternalNoteService

File: `src/SupportHub.Infrastructure/Services/InternalNoteService.cs`

Key behaviors:

- Validates that the current user is an agent (not a requester/external user). Return `Result.Failure("Unauthorized")` if not.
- Sets `AuthorId` from the current user context.
- Notes are ordered by `CreatedAt` ascending.
- Notes are never visible in external/requester-facing views.

### LocalFileStorageService

File: `src/SupportHub.Infrastructure/Services/LocalFileStorageService.cs`

Implements `IFileStorageService` for on-prem network share storage.

Key behaviors:

- Reads base path from configuration: `FileStorage:BasePath`.
- On `SaveFileAsync`: generate a unique stored file name using `{Guid}_{sanitizedOriginalName}`, organized in date-based subdirectories (`yyyy/MM/dd/`). Write the stream to the file system. Return the relative storage path.
- On `GetFileAsync`: combine base path + storage path, open a `FileStream` for reading. Return `Result.Failure` if file not found.
- On `DeleteFileAsync`: delete the physical file. Return `Result.Failure` if file not found.
- Sanitize file names to remove path traversal characters.

### AttachmentService

File: `src/SupportHub.Infrastructure/Services/AttachmentService.cs`

Key behaviors:

- **File Size Limit** — Configurable via `FileStorage:MaxFileSizeBytes` (default 25 MB). Reject with `Result.Failure` if exceeded.
- **Allowed Extensions** — Configurable via `FileStorage:AllowedExtensions` (default: `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.png`, `.jpg`, `.jpeg`, `.gif`, `.txt`, `.csv`, `.zip`, `.msg`, `.eml`). Reject with `Result.Failure` if not in list.
- Delegates actual file I/O to `IFileStorageService`.
- Records `TicketAttachment` entity in the database linking to the stored file path.
- `DownloadAttachmentAsync` retrieves the attachment record, calls `IFileStorageService.GetFileAsync`, and returns the stream with content type and original file name.

### CannedResponseService

File: `src/SupportHub.Infrastructure/Services/CannedResponseService.cs`

Key behaviors:

- Queries return company-specific responses AND global responses (where `CompanyId` is null).
- Results ordered by `SortOrder` ascending, then `Title`.
- Soft-delete support.
- Only active responses returned by default (filter `IsActive == true`).

### TagService

File: `src/SupportHub.Infrastructure/Services/TagService.cs`

Key behaviors:

- `AddTagAsync` normalizes the tag to lowercase/trimmed before saving. Checks for existing tag (case-insensitive) on the same ticket and returns `Result.Failure` if duplicate.
- `RemoveTagAsync` performs soft-delete on the matching `TicketTag`.
- `GetPopularTagsAsync` aggregates tags across tickets (optionally filtered by company), grouped and ordered by frequency descending, limited to `count`.

### DI Registration

In `SupportHub.Infrastructure` DI extension method:

```csharp
services.AddScoped<ITicketService, TicketService>();
services.AddScoped<ITicketMessageService, TicketMessageService>();
services.AddScoped<IInternalNoteService, InternalNoteService>();
services.AddScoped<IFileStorageService, LocalFileStorageService>();
services.AddScoped<IAttachmentService, AttachmentService>();
services.AddScoped<ICannedResponseService, CannedResponseService>();
services.AddScoped<ITagService, TagService>();
```

---

## Wave 5 — Blazor UI Pages & Components

All pages in **SupportHub.Web/Components/Pages**. All shared components in **SupportHub.Web/Components/Shared**. Use MudBlazor throughout.

### Page 1: Create Ticket (`/tickets/create`)

File: `src/SupportHub.Web/Components/Pages/Tickets/CreateTicket.razor`

Layout:
- `MudForm` with validation
- **Company Selector** — `MudSelect<Guid>` bound to user's accessible companies
- **System / Application** — `MudTextField` (optional, freeform or future autocomplete)
- **Issue Type** — `MudTextField` (optional)
- **Subject** — `MudTextField` with `Required="true"`, max length 500
- **Description** — `MudTextField` with `Lines="8"` (multiline), `Required="true"`
- **Priority** — `MudSelect<TicketPriority>`, default `Medium`
- **File Upload** — `MudFileUpload` with multiple file support, drag-and-drop, shows file list with remove option. Validate extensions and size client-side before upload.
- **Tags** — `MudChipSet` with `MudAutocomplete` for tag entry; calls `ITagService.GetPopularTagsAsync` for suggestions
- **Submit Button** — `MudButton` with loading state, calls `ITicketService.CreateTicketAsync`, then navigates to the ticket detail page on success. Displays `MudAlert` on failure.

### Page 2: Ticket List (`/tickets`)

File: `src/SupportHub.Web/Components/Pages/Tickets/TicketList.razor`

Layout:
- **Filter Bar** — Row of filter controls above the grid:
  - `MudSelect` for Status (multi-select or single with "All")
  - `MudSelect` for Priority
  - `MudSelect` for Company
  - `MudSelect` for Assigned Agent
  - `MudTextField` for search term (searches subject, requester, ticket number)
  - `MudDateRangePicker` for date range
  - Tag filter via `MudAutocomplete`
  - Clear filters button
- **Data Grid** — `MudDataGrid<TicketSummaryDto>` with:
  - Server-side pagination (`ServerData` callback calling `ITicketService.GetTicketsAsync`)
  - Sortable columns: Ticket Number, Subject, Status, Priority, Requester, Company, Agent, Created
  - Status column renders `TicketStatusChip`
  - Priority column renders `TicketPriorityChip`
  - Row click navigates to ticket detail
  - Configurable page size (10, 25, 50)
- **Create Ticket Button** — `MudFab` in top-right corner navigating to `/tickets/create`

### Page 3: Ticket Detail (`/tickets/{id:guid}`)

File: `src/SupportHub.Web/Components/Pages/Tickets/TicketDetail.razor`

Layout (two-column):

**Main Column (left, ~70%)**:
- **Header**: Ticket number, subject, `TicketStatusChip`, `TicketPriorityChip`, source badge, created date, requester name/email
- **Conversation Timeline** (`ConversationTimeline` component):
  - Chronological interleaving of `TicketMessage` and `InternalNote` entries
  - Inbound messages styled left-aligned with one background color
  - Outbound messages styled right-aligned with another background color
  - Internal notes styled with a distinct warning/yellow background and "Internal Note" label, visible only when user is an agent
  - Each entry shows: sender/author name, timestamp, body content
  - Attachments listed inline on messages that have them (with download links)
- **Reply Composer** (below timeline):
  - `MudTextField` multiline for reply body
  - Canned response inserter: `MudMenu` or `MudAutocomplete` that loads canned responses; selecting one inserts the body text into the reply field
  - `MudFileUpload` for attaching files to the reply
  - Send button (calls `ITicketMessageService.AddMessageAsync` + `IAttachmentService.UploadAttachmentAsync` for any files)
- **Internal Note Composer** (toggle/tab below timeline):
  - `MudTextField` multiline for note body
  - Add Note button (calls `IInternalNoteService.AddNoteAsync`)

**Sidebar (right, ~30%)**:
- **Properties Card** (`MudCard`):
  - Status — `MudSelect<TicketStatus>` with change handler calling `ITicketService.ChangeStatusAsync`
  - Priority — `MudSelect<TicketPriority>` with change handler calling `ITicketService.ChangePriorityAsync`
  - Assigned Agent — `MudSelect<Guid?>` populated with available agents, change handler calling `ITicketService.AssignTicketAsync`
  - Company — read-only display
  - System — editable `MudTextField`
  - Issue Type — editable `MudTextField`
  - Requester — read-only display (name + email)
  - Created — read-only timestamp
  - First Response — read-only timestamp (or "Awaiting")
  - Resolved — read-only timestamp (or dash)
  - Closed — read-only timestamp (or dash)
- **Tags Card** (`MudCard`):
  - Current tags displayed as `MudChip` elements with delete icon
  - `MudAutocomplete` to add new tags
- **Attachments Card** (`MudCard`):
  - List of all ticket-level attachments with file name, size, download link

### Page 4: Canned Responses Admin (`/admin/canned-responses`)

File: `src/SupportHub.Web/Components/Pages/Admin/CannedResponses.razor`

Layout:
- `MudDataGrid<CannedResponseDto>` with columns: Title, Category, Company (or "Global"), Sort Order, Active status, Actions
- Server-side pagination
- Filter by company and category
- **Create** button opens `MudDialog` with form: Title, Body (multiline), Company selector (optional), Category, Sort Order
- **Edit** action opens same dialog pre-populated
- **Delete** action with confirmation dialog (soft-delete)
- Toggle active/inactive via `MudSwitch` inline

### Shared Components

File: `src/SupportHub.Web/Components/Shared/TicketStatusChip.razor`

```razor
@* Renders a colored MudChip based on TicketStatus *@
<MudChip T="string" Color="@GetColor()" Size="Size.Small">@Status.ToString()</MudChip>

@code {
    [Parameter] public TicketStatus Status { get; set; }

    private Color GetColor() => Status switch
    {
        TicketStatus.New => Color.Info,
        TicketStatus.Open => Color.Primary,
        TicketStatus.Pending => Color.Warning,
        TicketStatus.OnHold => Color.Default,
        TicketStatus.Resolved => Color.Success,
        TicketStatus.Closed => Color.Dark,
        _ => Color.Default
    };
}
```

File: `src/SupportHub.Web/Components/Shared/TicketPriorityChip.razor`

```razor
@* Renders a colored MudChip based on TicketPriority *@
<MudChip T="string" Color="@GetColor()" Size="Size.Small">@Priority.ToString()</MudChip>

@code {
    [Parameter] public TicketPriority Priority { get; set; }

    private Color GetColor() => Priority switch
    {
        TicketPriority.Low => Color.Default,
        TicketPriority.Medium => Color.Info,
        TicketPriority.High => Color.Warning,
        TicketPriority.Urgent => Color.Error,
        _ => Color.Default
    };
}
```

File: `src/SupportHub.Web/Components/Shared/ConversationTimeline.razor`

- Accepts `IReadOnlyList<TicketMessageDto>` and `IReadOnlyList<InternalNoteDto>`
- Merges both lists into a single chronologically-ordered view by `CreatedAt`
- Uses a discriminated union or wrapper type to render the appropriate template for each entry
- Parameter `bool IsAgent` controls whether internal notes are visible

File: `src/SupportHub.Web/Components/Shared/FileUploadComponent.razor`

- Wraps `MudFileUpload` with:
  - Configurable max file size display
  - Allowed extension filtering (reads from configuration)
  - Client-side validation before upload
  - File list with name, size, remove button
  - Drag-and-drop zone

File: `src/SupportHub.Web/Components/Shared/TagInput.razor`

- `MudAutocomplete<string>` with debounced search calling `ITagService.GetPopularTagsAsync`
- On selection or Enter, adds tag as a `MudChip` to a displayed chip set
- Each chip has a close/remove button
- Exposes `IReadOnlyList<string> Tags` as a bindable parameter

---

## Wave 6 — API Controllers

All controllers in **SupportHub.Web/Controllers** (or a separate API project if split). Controllers use `[ApiController]`, return `IActionResult`, and delegate to service interfaces. All endpoints require `[Authorize]`.

### TicketsController

File: `src/SupportHub.Web/Controllers/TicketsController.cs`

```csharp
namespace SupportHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ITicketMessageService _messageService;
    private readonly IInternalNoteService _noteService;
    private readonly IAttachmentService _attachmentService;
    private readonly ITagService _tagService;

    // Constructor injection ...

    // GET api/tickets?status=Open&page=1&pageSize=25...
    [HttpGet]
    public async Task<IActionResult> GetTicketsAsync([FromQuery] TicketFilterRequest filter, CancellationToken ct)
    {
        var result = await _ticketService.GetTicketsAsync(filter, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // GET api/tickets/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicketByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.GetTicketByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    // POST api/tickets
    [HttpPost]
    public async Task<IActionResult> CreateTicketAsync([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.CreateTicketAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTicketByIdAsync), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.Error);
    }

    // PUT api/tickets/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTicketAsync(Guid id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.UpdateTicketAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // POST api/tickets/{id}/assign
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> AssignTicketAsync(Guid id, [FromBody] Guid agentId, CancellationToken ct)
    {
        var result = await _ticketService.AssignTicketAsync(id, agentId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    // POST api/tickets/{id}/status
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatusAsync(Guid id, [FromBody] TicketStatus newStatus, CancellationToken ct)
    {
        var result = await _ticketService.ChangeStatusAsync(id, newStatus, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    // POST api/tickets/{id}/messages
    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> AddMessageAsync(Guid id, [FromBody] CreateTicketMessageRequest request, CancellationToken ct)
    {
        var result = await _messageService.AddMessageAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // POST api/tickets/{id}/notes
    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNoteAsync(Guid id, [FromBody] CreateInternalNoteRequest request, CancellationToken ct)
    {
        var result = await _noteService.AddNoteAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // POST api/tickets/{id}/attachments
    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> UploadAttachmentAsync(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await _attachmentService.UploadAttachmentAsync(
            id, null, stream, file.FileName, file.ContentType, file.Length, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // GET api/tickets/{id}/attachments/{attachmentId}
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await _attachmentService.DownloadAttachmentAsync(attachmentId, ct);
        if (!result.IsSuccess) return NotFound(result.Error);
        var (fileStream, contentType, fileName) = result.Value;
        return File(fileStream, contentType, fileName);
    }

    // POST api/tickets/{id}/tags
    [HttpPost("{id:guid}/tags")]
    public async Task<IActionResult> AddTagAsync(Guid id, [FromBody] string tag, CancellationToken ct)
    {
        var result = await _tagService.AddTagAsync(id, tag, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // DELETE api/tickets/{id}/tags/{tag}
    [HttpDelete("{id:guid}/tags/{tag}")]
    public async Task<IActionResult> RemoveTagAsync(Guid id, string tag, CancellationToken ct)
    {
        var result = await _tagService.RemoveTagAsync(id, tag, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}
```

### CannedResponsesController

File: `src/SupportHub.Web/Controllers/CannedResponsesController.cs`

```csharp
namespace SupportHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CannedResponsesController : ControllerBase
{
    private readonly ICannedResponseService _cannedResponseService;

    // Constructor injection ...

    // GET api/cannedresponses?companyId={guid}&page=1&pageSize=25
    [HttpGet]
    public async Task<IActionResult> GetCannedResponsesAsync([FromQuery] Guid? companyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await _cannedResponseService.GetCannedResponsesAsync(companyId, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // POST api/cannedresponses
    [HttpPost]
    public async Task<IActionResult> CreateCannedResponseAsync([FromBody] CreateCannedResponseRequest request, CancellationToken ct)
    {
        var result = await _cannedResponseService.CreateCannedResponseAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(null, new { id = result.Value.Id }, result.Value) : BadRequest(result.Error);
    }

    // PUT api/cannedresponses/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCannedResponseAsync(Guid id, [FromBody] UpdateCannedResponseRequest request, CancellationToken ct)
    {
        var result = await _cannedResponseService.UpdateCannedResponseAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // DELETE api/cannedresponses/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCannedResponseAsync(Guid id, CancellationToken ct)
    {
        var result = await _cannedResponseService.DeleteCannedResponseAsync(id, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}
```

---

## Wave 7 — Tests

All tests in **SupportHub.Tests.Unit** using **xUnit**, **NSubstitute**, and **FluentAssertions**. Follow Arrange/Act/Assert pattern. Each service gets its own test class.

### TicketServiceTests

File: `tests/SupportHub.Tests.Unit/Services/TicketServiceTests.cs`

Test cases:

- `CreateTicketAsync_ValidRequest_ReturnsTicketWithGeneratedNumber` — Verify ticket number format `TKT-YYYYMMDD-NNNN`.
- `CreateTicketAsync_ValidRequest_SetsStatusToNew` — Confirm default status.
- `CreateTicketAsync_InvalidCompanyId_ReturnsFailure` — Company not found or not accessible.
- `GetTicketByIdAsync_ExistingTicket_ReturnsTicketDto` — Happy path retrieval.
- `GetTicketByIdAsync_InaccessibleCompany_ReturnsFailure` — Company isolation enforced.
- `GetTicketByIdAsync_DeletedTicket_ReturnsFailure` — Soft-deleted tickets not returned.
- `GetTicketsAsync_WithFilters_ReturnsFilteredResults` — Verify status, priority, company, search filters.
- `GetTicketsAsync_WithPagination_ReturnsCorrectPage` — Verify page/pageSize honored.
- `UpdateTicketAsync_ValidRequest_UpdatesFields` — Subject, description, priority changes.
- `AssignTicketAsync_ValidAgent_SetsAssignedAgentId` — Happy path assignment.
- `ChangeStatusAsync_ValidTransition_UpdatesStatus` — e.g., New -> Open.
- `ChangeStatusAsync_InvalidTransition_ReturnsFailure` — e.g., New -> Resolved.
- `ChangeStatusAsync_ToResolved_SetsResolvedAt` — Timestamp auto-set.
- `ChangeStatusAsync_ToClosed_SetsClosedAt` — Timestamp auto-set.
- `ChangeStatusAsync_ReopenFromResolved_ClearsResolvedAt` — Timestamp cleared on reopen.
- `DeleteTicketAsync_ExistingTicket_SoftDeletes` — Sets `IsDeleted` and `DeletedAt`.

### TicketMessageServiceTests

File: `tests/SupportHub.Tests.Unit/Services/TicketMessageServiceTests.cs`

Test cases:

- `AddMessageAsync_ValidRequest_ReturnsMessageDto` — Happy path.
- `AddMessageAsync_FirstOutboundMessage_SetsFirstResponseAt` — Timestamp tracking.
- `AddMessageAsync_SubsequentOutboundMessage_DoesNotOverwriteFirstResponseAt` — Idempotent.
- `AddMessageAsync_OutboundOnNewTicket_TransitionsToOpen` — Auto status change.
- `AddMessageAsync_NonExistentTicket_ReturnsFailure` — Validation.
- `GetMessagesAsync_ReturnsOrderedByCreatedAt` — Chronological order.

### InternalNoteServiceTests

File: `tests/SupportHub.Tests.Unit/Services/InternalNoteServiceTests.cs`

Test cases:

- `AddNoteAsync_AgentUser_ReturnsNoteDto` — Happy path.
- `AddNoteAsync_NonAgentUser_ReturnsFailure` — Authorization check.
- `AddNoteAsync_SetsAuthorIdFromCurrentUser` — Correct author assignment.
- `GetNotesAsync_ReturnsOrderedByCreatedAt` — Chronological order.
- `GetNotesAsync_NonExistentTicket_ReturnsFailure` — Validation.

### AttachmentServiceTests

File: `tests/SupportHub.Tests.Unit/Services/AttachmentServiceTests.cs`

Test cases:

- `UploadAttachmentAsync_ValidFile_ReturnsAttachmentDto` — Happy path with allowed extension and size.
- `UploadAttachmentAsync_ExceedsMaxSize_ReturnsFailure` — File size limit enforced.
- `UploadAttachmentAsync_DisallowedExtension_ReturnsFailure` — Extension validation.
- `UploadAttachmentAsync_WithMessageId_LinksToMessage` — Message-level attachment.
- `DownloadAttachmentAsync_ExistingAttachment_ReturnsStream` — Happy path download.
- `DownloadAttachmentAsync_NonExistentAttachment_ReturnsFailure` — Not found.
- `GetAttachmentsAsync_ReturnsAllForTicket` — List retrieval.

### CannedResponseServiceTests

File: `tests/SupportHub.Tests.Unit/Services/CannedResponseServiceTests.cs`

Test cases:

- `GetCannedResponsesAsync_WithCompanyId_ReturnsCompanyAndGlobal` — Company-specific + global results.
- `GetCannedResponsesAsync_WithoutCompanyId_ReturnsGlobalOnly` — Null company filter.
- `GetCannedResponsesAsync_OrdersBySortOrderThenTitle` — Correct ordering.
- `CreateCannedResponseAsync_ValidRequest_ReturnsDto` — Happy path.
- `UpdateCannedResponseAsync_ValidRequest_UpdatesFields` — Partial update.
- `DeleteCannedResponseAsync_ExistingResponse_SoftDeletes` — Soft-delete behavior.

### TagServiceTests

File: `tests/SupportHub.Tests.Unit/Services/TagServiceTests.cs`

Test cases:

- `AddTagAsync_NewTag_ReturnsTagDto` — Happy path.
- `AddTagAsync_DuplicateTagSameCase_ReturnsFailure` — Exact duplicate.
- `AddTagAsync_DuplicateTagDifferentCase_ReturnsFailure` — Case-insensitive uniqueness.
- `AddTagAsync_NormalizesToLowercase` — Tag stored lowercase/trimmed.
- `RemoveTagAsync_ExistingTag_SoftDeletes` — Soft-delete.
- `RemoveTagAsync_NonExistentTag_ReturnsFailure` — Not found.
- `GetPopularTagsAsync_ReturnsOrderedByFrequency` — Most-used tags first.
- `GetPopularTagsAsync_WithCompanyFilter_FiltersCorrectly` — Company scoping.

---

## Acceptance Criteria

- [ ] All Phase 2 entities created with proper EF configurations and no data annotations
- [ ] `AddCoreTicketing` migration runs successfully on the Phase 1 database
- [ ] Structured web form creates tickets with all required fields
- [ ] Ticket list displays with server-side filtering, pagination, and sorting
- [ ] Ticket detail shows full conversation timeline (messages + internal notes)
- [ ] File upload/download works end-to-end via network share storage
- [ ] Internal notes visible only to agents, never exposed to requesters
- [ ] Canned responses insertable into reply composer
- [ ] Tags can be added/removed from tickets with case-insensitive uniqueness
- [ ] Ticket number auto-generation works (`TKT-YYYYMMDD-NNNN` format)
- [ ] Company isolation enforced on all ticket queries
- [ ] Status transitions enforce valid state machine rules
- [ ] Status transitions update appropriate timestamps (`FirstResponseAt`, `ResolvedAt`, `ClosedAt`)
- [ ] API controllers return appropriate HTTP status codes and delegate to services
- [ ] All unit tests pass
- [ ] `dotnet build` completes with zero errors and zero warnings

---

## Dependencies

- **Phase 1 deliverables required**: `BaseEntity`, `Company`, `ApplicationUser`, `SupportHubDbContext`, Azure AD authentication, shell layout, `Result<T>`, `PagedResult<T>`, `ICurrentUserService`

---

## Configuration

Add to `appsettings.json`:

```json
{
  "FileStorage": {
    "BasePath": "\\\\server\\share\\supporthub\\attachments",
    "MaxFileSizeBytes": 26214400,
    "AllowedExtensions": ".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg,.gif,.txt,.csv,.zip,.msg,.eml"
  }
}
```

---

## Next Phase

**Phase 3 — Email Integration** adds Graph API mailbox polling, inbound email-to-ticket creation, outbound email replies, email threading via `X-SupportHub-TicketId` header, and AI-assisted classification. Phase 3 builds on the ticket and message infrastructure delivered here.
