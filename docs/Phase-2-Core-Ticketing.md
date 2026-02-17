# Phase 2 — Core Ticketing (Weeks 4–6)

> **Prerequisites:** Phase 0 (conventions) and Phase 1 (foundation) must be complete. Authentication, database, and company/user management are working.

---

## Objective

Build the complete ticket lifecycle: creation, assignment, status management, internal notes, file attachments, and canned responses. At the end of this phase, agents can manage tickets end-to-end through the Blazor UI and the API.

---

## Task 2.1 — Ticket Service

### Instructions

1. Create `ITicketService` in `Core/Interfaces/`:

```csharp
public interface ITicketService
{
    Task<Result<TicketDto>> CreateAsync(CreateTicketDto dto);
    Task<Result<TicketDto>> GetByIdAsync(int id);
    Task<Result<PagedResult<TicketListDto>>> GetListAsync(TicketFilterDto filter);
    Task<Result<TicketDto>> UpdateAsync(int id, UpdateTicketDto dto);
    Task<Result<TicketDto>> AssignAsync(int ticketId, int? agentId);
    Task<Result<TicketDto>> ChangeStatusAsync(int ticketId, TicketStatus newStatus);
    Task<Result<TicketDto>> ChangePriorityAsync(int ticketId, TicketPriority newPriority);
    Task<Result<bool>> SoftDeleteAsync(int id);
}
```

2. Create DTOs in `Core/DTOs/`:

   - **`TicketDto`** (full read model): Id, Subject, Status, Priority, Source, CompanyId, CompanyName, AssignedAgentId, AssignedAgentName, RequesterEmail, RequesterName, CreatedAt, UpdatedAt, FirstResponseAt, ResolvedAt, ClosedAt, Messages (list), InternalNotes (list), Attachments (list)
   - **`TicketListDto`** (grid row): Id, Subject, Status, Priority, Source, CompanyName, AssignedAgentName, RequesterName, RequesterEmail, CreatedAt, UpdatedAt, MessageCount, HasUnreadMessages
   - **`CreateTicketDto`**: CompanyId, Subject, Priority, RequesterEmail, RequesterName, InitialMessage (string), Source (defaults to Portal)
   - **`UpdateTicketDto`**: Subject, Priority
   - **`TicketFilterDto`**: CompanyId?, Status?, Priority?, AssignedAgentId?, SearchTerm?, DateFrom?, DateTo?, PageNumber (default 1), PageSize (default 25), SortBy, SortDirection

3. Create `PagedResult<T>` in `Core/DTOs/`:

```csharp
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
```

4. Implement `TicketService` in `Infrastructure/Services/`:

   **Business rules to enforce:**
   - On `CreateAsync`: Status defaults to `New`. Source defaults to `Portal` (email source handled in Phase 3). Create the initial `TicketMessage` from the `InitialMessage` field.
   - On `ChangeStatusAsync`:
     - Moving to `Resolved` → set `ResolvedAt = DateTimeOffset.UtcNow`
     - Moving to `Closed` → set `ClosedAt = DateTimeOffset.UtcNow`
     - Reopening (from Resolved/Closed → Open) → clear `ResolvedAt` and `ClosedAt`
   - On `AssignAsync`: If assigning for the first time and status is `New`, auto-change status to `Open`
   - All mutations verify company access using the current user's company assignments (except SuperAdmin)
   - `GetListAsync` must filter by the user's assigned companies (SuperAdmin sees all)
   - Use optimistic concurrency via `RowVersion` on updates — return a friendly error on conflict

5. Create `FluentValidation` validators:
   - `CreateTicketValidator`: Subject required (max 500), RequesterEmail valid format, CompanyId > 0
   - `UpdateTicketValidator`: Subject max 500 if provided

6. Write unit tests for `TicketService`:
   - Test creation with initial message
   - Test status transitions (valid and invalid)
   - Test assignment auto-changes status
   - Test company access enforcement
   - Test concurrency conflict handling

---

## Task 2.2 — Internal Notes

### Instructions

1. Create `IInternalNoteService` in `Core/Interfaces/`:

```csharp
public interface IInternalNoteService
{
    Task<Result<InternalNoteDto>> AddAsync(int ticketId, CreateInternalNoteDto dto);
    Task<Result<List<InternalNoteDto>>> GetByTicketIdAsync(int ticketId);
    Task<Result<bool>> DeleteAsync(int noteId);
}
```

2. Create DTOs:
   - `InternalNoteDto`: Id, TicketId, AuthorName, Body, CreatedAt
   - `CreateInternalNoteDto`: Body (required)

3. Implement `InternalNoteService`:
   - `AuthorId` comes from `ICurrentUserService`
   - Verify the user has access to the ticket's company
   - Only the author or an Admin/SuperAdmin can delete a note

---

## Task 2.3 — File Attachments

### Instructions

1. Create `IFileStorageService` in `Core/Interfaces/`:

```csharp
public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string contentType);
    Task<Stream> GetFileAsync(string storedFileName);
    Task<bool> DeleteFileAsync(string storedFileName);
}
```

2. Implement `LocalFileStorageService` in `Infrastructure/Storage/`:
   - Store files in `{StorageSettings.BasePath}/{YYYY}/{MM}/{GUID}_{originalFileName}`
   - Create subdirectories by year/month to avoid single-folder bloat
   - Validate file size against `StorageSettings.MaxFileSizeMb`
   - Inject `IOptions<StorageSettings>`

3. Create `IAttachmentService` in `Core/Interfaces/`:

```csharp
public interface IAttachmentService
{
    Task<Result<TicketAttachmentDto>> UploadAsync(int ticketId, int? messageId, Stream fileStream, string fileName, string contentType, long fileSize);
    Task<Result<(Stream FileStream, string ContentType, string FileName)>> DownloadAsync(int attachmentId);
    Task<Result<List<TicketAttachmentDto>>> GetByTicketIdAsync(int ticketId);
}
```

4. Create DTOs:
   - `TicketAttachmentDto`: Id, OriginalFileName, ContentType, FileSizeBytes, CreatedAt, DownloadUrl

5. Implement `AttachmentService`:
   - Validate allowed file extensions (configurable whitelist, e.g., `.pdf, .docx, .xlsx, .png, .jpg, .gif, .txt, .csv, .zip`)
   - Validate file size
   - Save via `IFileStorageService`, then create the `TicketAttachment` record
   - For download, verify company access

6. Register `StorageSettings` in `appsettings.json`:

```json
"StorageSettings": {
    "BasePath": "C:\\SupportHub\\Attachments",
    "MaxFileSizeMb": 25,
    "AllowedExtensions": ".pdf,.docx,.xlsx,.png,.jpg,.jpeg,.gif,.txt,.csv,.zip,.msg"
}
```

---

## Task 2.4 — Canned Responses

### Instructions

1. Create `ICannedResponseService` in `Core/Interfaces/`:

```csharp
public interface ICannedResponseService
{
    Task<Result<List<CannedResponseDto>>> GetByCompanyIdAsync(int companyId);
    Task<Result<List<CannedResponseDto>>> GetGlobalAsync();
    Task<Result<CannedResponseDto>> CreateAsync(CreateCannedResponseDto dto);
    Task<Result<CannedResponseDto>> UpdateAsync(int id, UpdateCannedResponseDto dto);
    Task<Result<bool>> DeleteAsync(int id);
}
```

2. Create DTOs:
   - `CannedResponseDto`: Id, CompanyId, CompanyName, Title, Body, SortOrder
   - `CreateCannedResponseDto`: CompanyId (null for global), Title, Body, SortOrder
   - `UpdateCannedResponseDto`: Title, Body, SortOrder

3. Implement `CannedResponseService`:
   - Global canned responses (CompanyId = null) are visible to all agents
   - Company-specific canned responses are only visible to agents assigned to that company
   - Only Admin/SuperAdmin can create/edit/delete
   - Support template variables (for future use): `{{ticket.requesterName}}`, `{{ticket.id}}`, `{{agent.name}}`

---

## Task 2.5 — Blazor UI: Ticket List Page

### Instructions

Create `Pages/Tickets/TicketList.razor` and `TicketList.razor.cs`:

1. **Layout:**
   - Top bar: Company selector dropdown (filtered to user's assigned companies, SuperAdmin sees all), "New Ticket" button
   - Filter bar: Status multi-select chips, Priority dropdown, Assigned Agent dropdown, Date range picker, Search text box
   - Main content: `MudDataGrid<TicketListDto>` with server-side pagination and sorting

2. **Grid Columns:**
   - ID (clickable link to detail)
   - Subject (truncated to ~60 chars)
   - Status (color-coded `MudChip`: New=blue, Open=green, AwaitingCustomer=orange, Resolved=gray, etc.)
   - Priority (icon + text: Urgent=red, High=orange, Medium=yellow, Low=gray)
   - Requester Name
   - Assigned Agent (show "Unassigned" if null with a distinct style)
   - Created Date (relative time, e.g., "2 hours ago", with tooltip showing full date)
   - Last Updated (relative time)

3. **Behavior:**
   - Default sort: most recently updated first
   - Clicking a row navigates to `TicketDetail`
   - Filters persist in the URL query string so the page is bookmarkable
   - Auto-refresh: poll the API every 60 seconds and update the grid if data changed (simple polling for v1)
   - Show a badge or count of "New" and "Unassigned" tickets in the sidebar nav

4. **Accessibility:**
   - All interactive elements have aria labels
   - Color-coded statuses also have text labels

---

## Task 2.6 — Blazor UI: Ticket Detail Page

### Instructions

Create `Pages/Tickets/TicketDetail.razor` and `TicketDetail.razor.cs`:

1. **Layout (two-column):**

   **Left column (70% width) — Conversation:**
   - Chronological list of `TicketMessage` items, styled differently for inbound (left-aligned, light background) vs outbound (right-aligned, themed background)
   - Each message shows: sender name, timestamp, body (rendered as HTML-safe text), attachments as download links
   - Internal notes interspersed in the timeline with a distinct visual style (e.g., yellow background, "Internal Note" label, lock icon). Only visible to agents — if the UI is ever customer-facing in the future, these are hidden.
   - Reply composer at the bottom:
     - Rich text area (MudBlazor `MudTextField` with multiline for v1, can upgrade to rich editor later)
     - "Insert Canned Response" button → opens a dialog/popover to search and select a canned response, which inserts the body into the reply field
     - Attachment upload: `MudFileUpload` supporting multiple files with drag-and-drop
     - "Reply" button and "Add Internal Note" button (side by side, visually distinct)
     - Sender selection: toggle between "Reply as [shared mailbox]" and "Reply as [my email]"

   **Right column (30% width) — Properties Panel:**
   - Status dropdown (with allowed transitions)
   - Priority dropdown
   - Assigned Agent dropdown (list of agents assigned to this company)
   - Requester info: name, email (read-only)
   - Company name (read-only)
   - Source badge (Email/Portal/API)
   - Created date, Last updated date
   - SLA status (placeholder for Phase 4 — show "SLA not configured" for now)
   - "Delete Ticket" button (soft-delete, with confirmation dialog, Admin/SuperAdmin only)

2. **Behavior:**
   - Property changes (status, priority, agent) save immediately via API call with optimistic concurrency
   - Show a `MudSnackbar` on successful save or on concurrency conflict ("This ticket was updated by another user. Please refresh.")
   - Reply/note submission clears the composer and scrolls to the new message
   - File uploads show progress indicators
   - Navigating away with unsaved reply text shows a "discard changes?" prompt

---

## Task 2.7 — Blazor UI: Create Ticket Dialog

### Instructions

Create a `MudDialog` component `CreateTicketDialog.razor`:

1. **Fields:**
   - Company (dropdown, required — pre-selected if user is on a company-filtered view)
   - Subject (text, required)
   - Priority (dropdown, defaults to Medium)
   - Requester Name (text, required — with autocomplete from existing requester emails in the company)
   - Requester Email (email, required)
   - Description / Initial Message (multiline text, required)
   - Attachments (file upload, optional)

2. **Behavior:**
   - Client-side validation with `FluentValidation`
   - On submit, call `ITicketService.CreateAsync` then navigate to the new ticket detail page
   - Show validation errors inline

---

## Task 2.8 — API Controllers

### Instructions

1. Create `TicketsController` in `Api/Controllers/v1/`:

```
GET    /api/v1/tickets           → GetList (with query params for filters)
GET    /api/v1/tickets/{id}      → GetById
POST   /api/v1/tickets           → Create
PUT    /api/v1/tickets/{id}      → Update
PATCH  /api/v1/tickets/{id}/assign         → Assign
PATCH  /api/v1/tickets/{id}/status         → ChangeStatus
PATCH  /api/v1/tickets/{id}/priority       → ChangePriority
DELETE /api/v1/tickets/{id}      → SoftDelete

POST   /api/v1/tickets/{id}/messages       → AddReply
POST   /api/v1/tickets/{id}/notes          → AddInternalNote
GET    /api/v1/tickets/{id}/notes          → GetNotes
DELETE /api/v1/tickets/{id}/notes/{noteId} → DeleteNote

POST   /api/v1/tickets/{id}/attachments    → Upload
GET    /api/v1/tickets/{id}/attachments/{attachmentId}  → Download
```

2. All endpoints require `[Authorize(Policy = "AgentOrAbove")]` minimum.
3. Delete endpoints require `[Authorize(Policy = "AdminOrAbove")]`.
4. Return `ProblemDetails` for validation errors and business rule violations.
5. Support `If-Match` header with `RowVersion` for optimistic concurrency on update operations.

2. Create `CannedResponsesController`:

```
GET    /api/v1/canned-responses?companyId={id}    → GetByCompany (includes global)
POST   /api/v1/canned-responses                   → Create (AdminOrAbove)
PUT    /api/v1/canned-responses/{id}               → Update (AdminOrAbove)
DELETE /api/v1/canned-responses/{id}               → Delete (AdminOrAbove)
```

---

## Task 2.9 — Navigation and Layout Updates

### Instructions

1. Update the Blazor `MainLayout` to include a sidebar with:
   - Dashboard (placeholder for Phase 5)
   - Tickets (with badge showing count of New/Unassigned)
   - Knowledge Base (placeholder, grayed out until Phase 5)
   - Admin section (visible to Admin/SuperAdmin):
     - Companies
     - Users
     - Canned Responses
     - SLA Policies (placeholder for Phase 4)
     - Reports (placeholder for Phase 5)

2. Add a top bar with:
   - App name/logo
   - Company context switcher (if user has multiple companies)
   - User profile dropdown (name, role, sign out)

3. Create a `CompanyContext` service that tracks the currently selected company:
   - Persisted in the session/circuit
   - Used by all pages to filter data
   - If a user has only one company, auto-select it and hide the switcher

---

## Acceptance Criteria for Phase 2

- [ ] Agents can create tickets via the portal with all required fields
- [ ] Ticket list page displays tickets with filtering, sorting, and pagination
- [ ] Ticket detail page shows full conversation thread with correct visual styling
- [ ] Agents can reply to tickets (message is saved, appears in timeline)
- [ ] Agents can add internal notes (visually distinct from messages)
- [ ] Agents can upload and download file attachments on tickets
- [ ] Agents can insert canned responses into replies
- [ ] Properties panel allows changing status, priority, and assignment with immediate save
- [ ] Optimistic concurrency prevents silent overwrites
- [ ] Company-level access control is enforced (agents only see tickets for their assigned companies)
- [ ] API endpoints work independently (testable via Swagger)
- [ ] All new services have unit tests
