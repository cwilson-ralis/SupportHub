# Phase 2 — Core Ticketing: COMPLETE

## Summary
All 7 waves of Phase 2 completed. Build: 0 errors, 0 warnings. Tests: 82/82 passing (48 new Phase 2 tests + 34 Phase 1 tests).

## Delivered Artifacts

### Domain (SupportHub.Domain)
- `Enums/TicketStatus.cs` — New, Open, Pending, OnHold, Resolved, Closed
- `Enums/TicketPriority.cs` — Low, Medium, High, Urgent
- `Enums/TicketSource.cs` — WebForm, Email, Api, Internal
- `Enums/MessageDirection.cs` — Inbound, Outbound
- `Entities/Ticket.cs` — Core ticket with CompanyId, QueueId (nullable), TicketNumber, Status, Priority, Source, RequesterEmail/Name, AssignedAgentId, System, IssueType, lifecycle timestamps, AiClassification
- `Entities/TicketMessage.cs` — Direction, SenderEmail/Name, Body, HtmlBody, ExternalMessageId
- `Entities/TicketAttachment.cs` — TicketId, TicketMessageId (nullable), FileName, OriginalFileName, ContentType, FileSize, StoragePath
- `Entities/InternalNote.cs` — TicketId, AuthorId, Body
- `Entities/TicketTag.cs` — TicketId, Tag
- `Entities/CannedResponse.cs` — CompanyId (nullable=global), Title, Body, Category, SortOrder, IsActive

### Application (SupportHub.Application)
- `DTOs/TicketDtos.cs` — TicketDto, TicketSummaryDto, CreateTicketRequest, UpdateTicketRequest, TicketFilterRequest
- `DTOs/TicketMessageDtos.cs` — TicketMessageDto, CreateTicketMessageRequest
- `DTOs/TicketAttachmentDto.cs` — TicketAttachmentDto
- `DTOs/InternalNoteDtos.cs` — InternalNoteDto, CreateInternalNoteRequest
- `DTOs/TicketTagDto.cs` — TicketTagDto
- `DTOs/CannedResponseDtos.cs` — CannedResponseDto, CreateCannedResponseRequest, UpdateCannedResponseRequest
- `Interfaces/ITicketService.cs` — CRUD + Assign + ChangeStatus + ChangePriority
- `Interfaces/ITicketMessageService.cs` — AddMessage + GetMessages
- `Interfaces/IInternalNoteService.cs` — AddNote + GetNotes
- `Interfaces/IFileStorageService.cs` — SaveFile + GetFile + DeleteFile
- `Interfaces/IAttachmentService.cs` — Upload + Download + GetAttachments
- `Interfaces/ICannedResponseService.cs` — Paged list + CRUD
- `Interfaces/ITagService.cs` — Add + Remove + GetPopular

### Infrastructure (SupportHub.Infrastructure)
- `Data/Configurations/TicketConfiguration.cs` — TicketNumber unique, (CompanyId,Status) composite index
- `Data/Configurations/TicketMessageConfiguration.cs` — ExternalMessageId index
- `Data/Configurations/TicketAttachmentConfiguration.cs` — TicketId + TicketMessageId indexes
- `Data/Configurations/InternalNoteConfiguration.cs` — AuthorId FK to ApplicationUser
- `Data/Configurations/TicketTagConfiguration.cs` — Composite unique (TicketId, Tag)
- `Data/Configurations/CannedResponseConfiguration.cs` — Nullable CompanyId FK
- `Data/Migrations/20260219051431_AddCoreTicketing.cs` — Creates all 6 tables
- `Services/TicketService.cs` — Full implementation with ticket numbering, isolation, state machine
- `Services/TicketMessageService.cs` — FirstResponseAt tracking, auto New→Open transition
- `Services/InternalNoteService.cs` — Role validation, agent-only notes
- `Services/LocalFileStorageService.cs` — Date-based file storage, sanitized names
- `Services/AttachmentService.cs` — Extension/size validation, delegates to IFileStorageService
- `Services/CannedResponseService.cs` — Company+global results, active filter, SortOrder ordering
- `Services/TagService.cs` — Lowercase normalization, soft-delete removal, frequency aggregation
- `DependencyInjection.cs` — All 7 new services registered

### Web (SupportHub.Web)
- `Components/Pages/Tickets/CreateTicket.razor` — MudForm web intake (/tickets/create)
- `Components/Pages/Tickets/TicketList.razor` — Server-side data grid with filters (/tickets)
- `Components/Pages/Tickets/TicketDetail.razor` — Two-column detail view (/tickets/{id})
- `Components/Pages/Admin/CannedResponses.razor` — Admin CRUD (/admin/canned-responses)
- `Components/Pages/Admin/CannedResponseFormDialog.razor` — Create/edit dialog
- `Components/Shared/TicketStatusChip.razor` — Color-coded status chip
- `Components/Shared/TicketPriorityChip.razor` — Color-coded priority chip
- `Components/Shared/ConversationTimeline.razor` — Merged message+note timeline
- `Components/Shared/TagInput.razor` — Autocomplete tag input with chip display
- `Components/Layout/NavMenu.razor` — Added Tickets + Canned Responses nav links
- `Components/_Imports.razor` — Added SupportHub.Web.Components.Shared using
- `Controllers/TicketsController.cs` — 15 REST endpoints
- `Controllers/CannedResponsesController.cs` — 4 REST endpoints
- `appsettings.json` — FileStorage config added (BasePath, MaxFileSizeBytes, AllowedExtensions)

### Tests (SupportHub.Tests.Unit)
- `Services/TicketServiceTests.cs` — 16 tests (ticket number, status machine, isolation, lifecycle)
- `Services/TicketMessageServiceTests.cs` — 6 tests (FirstResponseAt, auto-transition)
- `Services/InternalNoteServiceTests.cs` — 5 tests (role check, author, ordering)
- `Services/AttachmentServiceTests.cs` — 7 tests (size/ext validation, upload, download)
- `Services/CannedResponseServiceTests.cs` — 6 tests (company+global, ordering, CRUD)
- `Services/TagServiceTests.cs` — 8 tests (normalization, dedup, soft-delete, frequency)

## Key Technical Decisions Made
- Services use C# 12 primary constructor syntax (e.g., `public class TicketService(SupportHubDbContext _context, ...) : ITicketService`)
- DbContext field name: `_context` (not `_dbContext`)
- `BaseEntity.UpdatedAt` (not `ModifiedAt`) — TicketSummaryDto maps to `t.UpdatedAt`
- Status transition table stored as `Dictionary<TicketStatus, HashSet<TicketStatus>>`
- Reopening from Resolved OR Closed clears BOTH `ResolvedAt` AND `ClosedAt`
- `LocalFileStorageService` stores files at `{BasePath}/yyyy/MM/dd/{Guid}_{sanitizedFileName}`; returns relative path as StoragePath
- FileStorage config in appsettings uses `C:\\SupportHubFiles` as dev BasePath (not a network share)
- Canned responses: null companyId query returns ONLY global; companyId query returns company+global
- InMemory DB does not enforce HasQueryFilter — tests use `IgnoreQueryFilters()` to verify soft-deletes

## What Phase 3 Needs from Phase 2
- Ticket entity and TicketMessage entity (email threading, ExternalMessageId)
- ITicketService.CreateTicketAsync (email-to-ticket creation)
- ITicketMessageService.AddMessageAsync (email reply dispatch)
- SupportHubDbContext with all ticket DbSets
- TicketSource.Email enum value
- DependencyInjection.cs (add email service registrations)
