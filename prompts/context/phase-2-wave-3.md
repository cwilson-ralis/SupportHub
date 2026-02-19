# Phase 2 Wave 3 — Service Interfaces & DTOs

## Completed

### DTOs (`src/SupportHub.Application/DTOs/`)
- `TicketDtos.cs` — TicketDto, TicketSummaryDto, CreateTicketRequest, UpdateTicketRequest, TicketFilterRequest
- `TicketMessageDtos.cs` — TicketMessageDto, CreateTicketMessageRequest
- `TicketAttachmentDto.cs` — TicketAttachmentDto
- `InternalNoteDtos.cs` — InternalNoteDto, CreateInternalNoteRequest
- `TicketTagDto.cs` — TicketTagDto
- `CannedResponseDtos.cs` — CannedResponseDto, CreateCannedResponseRequest, UpdateCannedResponseRequest

### Service Interfaces (`src/SupportHub.Application/Interfaces/`)
- `ITicketService.cs` — CreateTicketAsync, GetTicketByIdAsync, GetTicketsAsync, UpdateTicketAsync, AssignTicketAsync, ChangeStatusAsync, ChangePriorityAsync, DeleteTicketAsync
- `ITicketMessageService.cs` — AddMessageAsync, GetMessagesAsync
- `IInternalNoteService.cs` — AddNoteAsync, GetNotesAsync
- `IFileStorageService.cs` — SaveFileAsync, GetFileAsync, DeleteFileAsync
- `IAttachmentService.cs` — UploadAttachmentAsync, DownloadAttachmentAsync, GetAttachmentsAsync
- `ICannedResponseService.cs` — GetCannedResponsesAsync, CreateCannedResponseAsync, UpdateCannedResponseAsync, DeleteCannedResponseAsync
- `ITagService.cs` — AddTagAsync, RemoveTagAsync, GetPopularTagsAsync

## New Interfaces Available
- `ITicketService` in `SupportHub.Application.Interfaces`
- `ITicketMessageService` in `SupportHub.Application.Interfaces`
- `IInternalNoteService` in `SupportHub.Application.Interfaces`
- `IFileStorageService` in `SupportHub.Application.Interfaces`
- `IAttachmentService` in `SupportHub.Application.Interfaces`
- `ICannedResponseService` in `SupportHub.Application.Interfaces`
- `ITagService` in `SupportHub.Application.Interfaces`

## Key DTO Notes
- `TicketDto` uses enum types (TicketStatus, TicketPriority, TicketSource) — not strings
- `TicketSummaryDto` is the lightweight list projection (includes MessageCount, no Tags/Attachments collections)
- `TicketFilterRequest` supports: CompanyId, Status, Priority, AssignedAgentId, SearchTerm, Tags, DateFrom, DateTo, Page, PageSize
- `IAttachmentService.DownloadAttachmentAsync` returns `Result<(Stream FileStream, string ContentType, string FileName)>`
- All service methods return `Task<Result<T>>` with `CancellationToken ct = default`

## Notes for Wave 4 (Service Implementations)

### TicketService key behaviors:
- Ticket number format: `TKT-{YYYYMMDD}-{NNNN}` (4-digit sequential per day, query last to find next)
- Company isolation: filter all queries by user's accessible companies (via ICurrentUserService.HasAccessToCompanyAsync)
- Status transitions (enforce these exactly):
  - New → Open, Pending, Closed
  - Open → Pending, OnHold, Resolved, Closed
  - Pending → Open, OnHold, Resolved, Closed
  - OnHold → Open, Pending, Resolved, Closed
  - Resolved → Open, Closed
  - Closed → Open (reopen only)
- Auto-timestamps: ResolvedAt set on→Resolved, ClosedAt set on→Closed, both cleared on→Open (reopen)
- FirstResponseAt: set by TicketMessageService on first Outbound message (not by status change)

### TicketMessageService key behaviors:
- On first Outbound message: set Ticket.FirstResponseAt (if still null)
- If Ticket.Status == New and Outbound message added: auto-transition to Open
- Return messages ordered by CreatedAt ascending

### InternalNoteService key behaviors:
- Validate current user is Agent or Admin/SuperAdmin (use ICurrentUserService.GetUserRolesAsync)
- Set AuthorId from ICurrentUserService.UserId
- Return notes ordered by CreatedAt ascending

### LocalFileStorageService key behaviors:
- Read base path from config: `FileStorage:BasePath`
- Store files as: `{BasePath}/yyyy/MM/dd/{Guid}_{sanitizedFileName}`
- Return relative path (not full path) as StoragePath

### AttachmentService key behaviors:
- Max size: `FileStorage:MaxFileSizeBytes` (default 26214400 = 25MB)
- Allowed extensions: `FileStorage:AllowedExtensions` (comma-separated)
- Delegate I/O to IFileStorageService, record TicketAttachment entity in DB

### CannedResponseService key behaviors:
- GetCannedResponsesAsync returns company-specific AND global (CompanyId null) responses
- Only active responses by default (IsActive == true)
- Ordered by SortOrder asc, then Title

### TagService key behaviors:
- Normalize tag to lowercase + trimmed before save
- Check for existing tag (case-insensitive) on same ticket → return Failure if duplicate
- RemoveTagAsync does soft-delete (not physical delete)
- GetPopularTagsAsync aggregates by frequency, ordered descending

### DI registrations to add in DependencyInjection.cs:
```csharp
services.AddScoped<ITicketService, TicketService>();
services.AddScoped<ITicketMessageService, TicketMessageService>();
services.AddScoped<IInternalNoteService, InternalNoteService>();
services.AddScoped<IFileStorageService, LocalFileStorageService>();
services.AddScoped<IAttachmentService, AttachmentService>();
services.AddScoped<ICannedResponseService, CannedResponseService>();
services.AddScoped<ITagService, TagService>();
```

## Build Status at Wave 3 Completion
- `dotnet build`: 0 errors, 0 warnings
- `dotnet test`: 34/34 passing
