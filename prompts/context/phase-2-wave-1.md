# Phase 2 Wave 1 — Domain Entities & Enums

## Completed
- `src/SupportHub.Domain/Enums/TicketStatus.cs` — New, Open, Pending, OnHold, Resolved, Closed
- `src/SupportHub.Domain/Enums/TicketPriority.cs` — Low, Medium, High, Urgent
- `src/SupportHub.Domain/Enums/TicketSource.cs` — WebForm, Email, Api, Internal
- `src/SupportHub.Domain/Enums/MessageDirection.cs` — Inbound, Outbound
- `src/SupportHub.Domain/Entities/Ticket.cs` — Full entity with all fields and navigation properties
- `src/SupportHub.Domain/Entities/TicketMessage.cs` — Direction, Body, HtmlBody, ExternalMessageId, Attachments nav
- `src/SupportHub.Domain/Entities/TicketAttachment.cs` — TicketId, TicketMessageId (nullable), FileName, OriginalFileName, ContentType, FileSize, StoragePath
- `src/SupportHub.Domain/Entities/InternalNote.cs` — TicketId, AuthorId, Body; nav to Ticket + ApplicationUser
- `src/SupportHub.Domain/Entities/TicketTag.cs` — TicketId, Tag; nav to Ticket
- `src/SupportHub.Domain/Entities/CannedResponse.cs` — CompanyId (nullable = global), Title, Body, Category, SortOrder, IsActive
- `src/SupportHub.Infrastructure/Data/SupportHubDbContext.cs` — All 6 new DbSets already added

## New Types Available
- `SupportHub.Domain.Enums.TicketStatus` — enum (New, Open, Pending, OnHold, Resolved, Closed)
- `SupportHub.Domain.Enums.TicketPriority` — enum (Low, Medium, High, Urgent)
- `SupportHub.Domain.Enums.TicketSource` — enum (WebForm, Email, Api, Internal)
- `SupportHub.Domain.Enums.MessageDirection` — enum (Inbound, Outbound)
- `SupportHub.Domain.Entities.Ticket` — core ticket entity
- `SupportHub.Domain.Entities.TicketMessage` — message entity
- `SupportHub.Domain.Entities.TicketAttachment` — attachment entity
- `SupportHub.Domain.Entities.InternalNote` — internal note entity
- `SupportHub.Domain.Entities.TicketTag` — tag entity
- `SupportHub.Domain.Entities.CannedResponse` — canned response entity

## Notes for Next Wave (Wave 2 — EF Configurations & Migration)
- DbContext already has all 6 DbSets — no changes needed there
- Only Phase 1 EF configs exist in Data/Configurations/ — all 6 Phase 2 configs still needed
- Company entity does NOT have a `Tickets` navigation — use `.WithMany()` (not `.WithMany(c => c.Tickets)`)
- All Phase 2 entities inherit BaseEntity — global soft-delete filter via `HasQueryFilter(x => !x.IsDeleted)` must be on each config
- TicketTag requires composite unique index on `(TicketId, Tag)`
- TicketNumber unique index required on Ticket
- All FK relationships use `OnDelete(DeleteBehavior.Restrict)` per design spec
- Enums stored as strings: Status, Priority, Source (on Ticket); Direction (on TicketMessage)
- Migration name: `AddCoreTicketing`

## Build Status at Wave 1 Completion
- `dotnet build`: 0 errors, 0 warnings
- `dotnet test`: 34/34 passing
