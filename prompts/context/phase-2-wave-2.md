# Phase 2 Wave 2 — EF Configurations & Migration

## Completed
- `src/SupportHub.Infrastructure/Data/Configurations/TicketConfiguration.cs` — Tickets table, composite index (CompanyId,Status), unique index on TicketNumber, all FK relationships with Restrict
- `src/SupportHub.Infrastructure/Data/Configurations/TicketMessageConfiguration.cs` — TicketMessages table, Direction as string, ExternalMessageId index
- `src/SupportHub.Infrastructure/Data/Configurations/TicketAttachmentConfiguration.cs` — TicketAttachments table, indexes on TicketId and TicketMessageId
- `src/SupportHub.Infrastructure/Data/Configurations/InternalNoteConfiguration.cs` — InternalNotes table, FK to ApplicationUser (Author) Restrict
- `src/SupportHub.Infrastructure/Data/Configurations/TicketTagConfiguration.cs` — TicketTags table, composite UNIQUE index (TicketId, Tag)
- `src/SupportHub.Infrastructure/Data/Configurations/CannedResponseConfiguration.cs` — CannedResponses table, nullable FK to Company
- `src/SupportHub.Infrastructure/Data/Migrations/20260219051431_AddCoreTicketing.cs` — Migration creating all 6 tables

## Key Technical Notes
- Namespace: `SupportHub.Infrastructure.Data.Configurations` (matches existing config files)
- All configs apply `HasQueryFilter(x => !x.IsDeleted)` for soft-delete
- All FK relationships use `OnDelete(DeleteBehavior.Restrict)` — no cascades
- Enums stored as strings: Status/Priority/Source (Ticket), Direction (TicketMessage)
- Company.Tickets nav does not exist — TicketConfiguration uses `.WithMany()` (no lambda)
- Migration generates tables in dependency order (no FK violations)

## Notes for Next Wave (Wave 4 — Service Implementations)
- All interfaces are now in SupportHub.Application.Interfaces
- All DTOs are now in SupportHub.Application.DTOs
- DbContext already has all DbSets (from Wave 1)
- Service implementations need: SupportHubDbContext, ILogger<T>, ICurrentUserService, IAuditService
- ICurrentUserService provides: UserId, GetUserRolesAsync, HasAccessToCompanyAsync
- IAuditService.LogAsync for audit trail entries
- FileStorage config key: `FileStorage:BasePath`, `FileStorage:MaxFileSizeBytes`, `FileStorage:AllowedExtensions`
- LocalFileStorageService goes in `src/SupportHub.Infrastructure/Services/` (not Infrastructure/Storage/)

## Build Status at Wave 2 Completion
- `dotnet build`: 0 errors, 0 warnings
- `dotnet test`: 34/34 passing
