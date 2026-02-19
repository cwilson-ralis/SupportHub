# Phase 7 - Polish & Hardening (Week 13+)

> **Prerequisites:** Phases 0-6 complete. All core features are functional. This phase focuses on production readiness, performance, UX polish, audit logging, and documentation.

---

## Objective

Prepare Ralis Support Hub for production use: add audit logging, improve error handling and resilience, optimize performance, polish the UI, and document everything. At the end of this phase, the system is ready for daily use by the support team.

---

## Task 7.1 - Audit Logging Completion and Coverage Hardening

### Instructions

1. **Validate and finalize `AuditLog` entity** in `Core/Entities/` (introduced in Phase 1):

```csharp
public class AuditLog
{
 public long Id { get; set; } // Use long - audit logs grow fast
 public DateTimeOffset Timestamp { get; set; }
 public string UserId { get; set; } // Azure AD Object ID
 public string UserDisplayName { get; set; }
 public string Action { get; set; } // e.g., "Ticket.Create", "Ticket.ChangeStatus"
 public string EntityType { get; set; } // e.g., "Ticket", "Company"
 public int? EntityId { get; set; }
 public string? OldValues { get; set; } // JSON serialized
 public string? NewValues { get; set; } // JSON serialized
 public string? AdditionalInfo { get; set; } // JSON, any extra context
 public string? IpAddress { get; set; }
}
```

 - Do NOT inherit from `BaseEntity` - audit logs are append-only, never soft-deleted
 - Store in its own table with a clustered index on `Timestamp DESC`
 - Add index on `EntityType + EntityId` for lookup
 - Add index on `UserId` for user history
 - If Phase 1 already created this table, use additive migrations only (no destructive rebuild)

2. **Create `IAuditService`** in `Core/Interfaces/`:

```csharp
public interface IAuditService
{
 Task LogAsync(string action, string entityType, int? entityId,
 object? oldValues = null, object? newValues = null, string? additionalInfo = null);
 Task<PagedResult<AuditLogDto>> SearchAsync(AuditSearchDto search);
}
```

3. **Implement `AuditService`** in `Infrastructure/Services/`:
 - Serialize old/new values to JSON using `System.Text.Json`
 - Get user info from `ICurrentUserService`
 - Get IP address from `IHttpContextAccessor`
 - Use a **separate DbContext** or `DbSet` to write audit logs - do NOT let audit logging interfere with the main transaction (fire-and-forget pattern with a channel/queue, or just a separate `SaveChangesAsync` call)

4. **Add audit logging calls** to all service methods that mutate data:

 | Action | Entity | Details |
 |---|---|---|
 | `Ticket.Create` | Ticket | New ticket values |
 | `Ticket.Update` | Ticket | Old -> new for changed fields |
 | `Ticket.ChangeStatus` | Ticket | Old status -> new status |
 | `Ticket.ChangePriority` | Ticket | Old priority -> new priority |
 | `Ticket.Assign` | Ticket | Old agent -> new agent |
 | `Ticket.Delete` | Ticket | Soft-delete |
 | `Ticket.Reply` | TicketMessage | Message ID, direction |
 | `Ticket.AddNote` | InternalNote | Note ID |
 | `Ticket.AddAttachment` | TicketAttachment | File name, size |
 | `Company.Create` | Company | New values |
 | `Company.Update` | Company | Changed fields |
 | `User.RoleChange` | UserProfile | Old role -> new role |
 | `User.CompanyAssign` | UserCompanyAssignment | Company ID |
 | `User.CompanyRemove` | UserCompanyAssignment | Company ID |
 | `SlaPolicy.Update` | SlaPolicy | Old -> new targets |
 | `KbArticle.Create` | KnowledgeBaseArticle | Article ID |
 | `KbArticle.Update` | KnowledgeBaseArticle | Changed fields |
 | `CannedResponse.Create` | CannedResponse | New values |
 | `Satisfaction.Submit` | CustomerSatisfactionRating | Score |

5. **Create `Pages/Admin/AuditLog.razor`:**
 - SuperAdmin only
 - Filters: Date range, User, Action, Entity Type, Entity ID
 - `MudDataGrid` with columns: Timestamp, User, Action, Entity, Details (expandable row showing old/new JSON)
 - Default: last 24 hours, newest first

---

## Task 7.2 - Error Handling & Resilience

### Instructions

1. **API Global Exception Handler:**
 - Ensure the middleware from Phase 1 catches all unhandled exceptions
 - Map known exception types:
 - `DbUpdateConcurrencyException` -> 409 Conflict with ProblemDetails
 - `UnauthorizedAccessException` -> 403 Forbidden
 - `FluentValidation.ValidationException` -> 400 Bad Request with field-level errors
 - All others -> 500 Internal Server Error (log full exception, return generic message)
 - Never expose stack traces or internal details in production responses

2. **Blazor Global Error Handling:**
 - Wrap all pages in an `ErrorBoundary` with a user-friendly fallback UI
 - Show `MudSnackbar` errors for recoverable failures (network issues, validation errors)
 - Show a full-page error for unrecoverable failures with a "Refresh" button
 - Log all errors via `ILogger`

3. **Retry Policies with Polly:**
 - Add `Microsoft.Extensions.Http.Polly` NuGet package
 - Configure retry policies for:
 - Microsoft Graph API calls: 3 retries with exponential backoff (1s, 2s, 4s), retry on 429/5xx
 - Database transient failures: 3 retries with 500ms delay
 - Add a circuit breaker on Graph API: break after 5 consecutive failures, stay open for 30 seconds

4. **Hangfire Job Resilience:**
 - Configure retry attributes on all Hangfire jobs: `[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]`
 - Failed jobs are visible in the Hangfire dashboard for manual retry
 - Add a Hangfire filter that logs job start/end/failure

---

## Task 7.3 - Performance Optimization

### Instructions

1. **Database Query Optimization:**
 - Review all EF Core queries in services - ensure no N+1 problems
 - Use `.AsNoTracking()` for all read-only queries
 - Use `.AsSplitQuery()` for queries with multiple collection includes
 - Add database indexes identified by reviewing slow queries:
 - `IX_Ticket_CompanyId_Status` (composite, for filtered ticket lists)
 - `IX_Ticket_AssignedAgentId` (for agent workload queries)
 - `IX_Ticket_CreatedAt` (for date-range reporting)
 - `IX_TicketMessage_TicketId_CreatedAt` (for conversation loading)
 - `IX_AuditLog_Timestamp` (descending, for recent audit queries)
 - Generate an EF migration for the new indexes

2. **Caching:**
 - Add `IMemoryCache` for data that changes infrequently:
 - Company list (cache for 5 minutes)
 - SLA policies per company (cache for 5 minutes, invalidate on update)
 - Canned responses per company (cache for 5 minutes, invalidate on update)
 - User company assignments (cache for 5 minutes, invalidate on change)
 - Create a `CacheService` wrapper with typed cache keys and consistent TTL patterns
 - Cache invalidation: when a service updates cached data, evict the relevant cache key

3. **Blazor Performance:**
 - Use `@key` directive on list-rendered components for efficient diffing
 - Use `ShouldRender()` override on heavy components to prevent unnecessary re-renders
 - Lazy-load report data (don't fetch all tabs at once - load on tab activation)
 - Debounce search inputs (500ms) to prevent excessive server calls
 - Use `virtualization` on long lists (`MudDataGrid` supports this)

4. **File Upload Performance:**
 - Stream large files directly to disk - do NOT buffer entire files in memory
 - Set `Kestrel` max request body size to match `StorageSettings.MaxFileSizeMb` + buffer

---

## Task 7.4 - UI Polish

### Instructions

1. **Theming:**
 - Configure MudBlazor theme with consistent colors:
 - Primary: a professional blue
 - Secondary: a complementary accent
 - Error: red for warnings/breaches
 - Warning: amber for SLA warnings
 - Success: green for met SLAs and confirmations
 - Dark mode support (MudBlazor built-in toggle)
 - Set it up so each Company can optionally have a small color accent or icon to visually distinguish in the UI

2. **Loading States:**
 - Every page/component that fetches data must show a loading indicator:
 - `MudProgressLinear` at the top of the page for initial load
 - `MudSkeleton` for content placeholders
 - `MudOverlay` with spinner for modal/dialog actions
 - Never show a blank page while data is loading

3. **Empty States:**
 - Every list/grid must have a meaningful empty state:
 - No tickets: "No tickets found. Adjust your filters or create a new ticket."
 - No KB articles: "No articles yet. Create your first knowledge base article."
 - No audit logs: "No activity in the selected time range."
 - Use `MudAlert` with `Info` severity or a centered illustration + text

4. **Responsive Design:**
 - The app will primarily be used on desktop, but ensure:
 - Sidebar collapses to a hamburger menu on narrow viewports
 - Ticket detail two-column layout stacks vertically on small screens
 - Data grids have horizontal scroll on small screens
 - Dialog/modal widths are responsive

5. **Keyboard Navigation:**
 - Ticket list: Enter opens selected ticket
 - Reply composer: Ctrl+Enter submits the reply
 - Search boxes: Escape clears, Enter submits
 - Modal dialogs: Escape closes

6. **Notifications and Feedback:**
 - Use `MudSnackbar` consistently:
 - Green: Success ("Ticket created", "Reply sent")
 - Red: Error ("Failed to send email", "Concurrency conflict")
 - Amber: Warning ("SLA approaching breach")
 - Blue: Info ("Ticket assigned to you")
 - Auto-dismiss success/info after 3 seconds
 - Errors persist until manually dismissed

---

## Task 7.5 - Security Hardening

### Instructions

1. **Input Validation:**
 - All user inputs are validated on both client (Blazor forms) and server (FluentValidation)
 - HTML content from emails is sanitized before rendering (`HtmlSanitizer`)
 - File uploads validate extension, content type, and perform basic magic byte checks
 - SQL injection: already handled by EF Core parameterized queries - verify no raw SQL

2. **Authorization Review:**
 - Audit every endpoint and Blazor page for correct authorization policy
 - Ensure company-level access checks cannot be bypassed by direct API calls
 - Verify that the rating page (public) does not leak ticket details beyond what's necessary

3. **Data Protection:**
 - Use `IDataProtectionProvider` to encrypt/sign the satisfaction survey tokens
 - Ensure connection strings and secrets are not in `appsettings.json` for production - use environment variables or a secrets manager
 - Add `[SensitiveData]` attribute or equivalent logging exclusion for email bodies and personal data

4. **HTTP Security Headers:**
 - Add middleware to set:
 - `X-Content-Type-Options: nosniff`
 - `X-Frame-Options: DENY`
 - `Referrer-Policy: strict-origin-when-cross-origin`
 - `Content-Security-Policy` appropriate for Blazor Web App (Server interactivity)
 - Enforce HTTPS in production

5. **Rate Limiting:**
 - Verify and tune rate limiting on the public rating endpoint (baseline implemented in Phase 5)
 - Add rate limiting on the API (use ASP.NET Core built-in rate limiting middleware)

---

## Task 7.6 - Health Checks & Monitoring

### Instructions

1. **Expand health checks** (from Phase 1):

```csharp
builder.Services.AddHealthChecks()
 .AddSqlServer(connectionString, name: "database")
 .AddHangfire(options => { options.MinimumAvailableServers = 1; }, name: "hangfire")
 .AddCheck<GraphApiHealthCheck>("graph-api") // Custom check that verifies Graph API auth
 .AddCheck<FileStorageHealthCheck>("file-storage"); // Custom check that verifies write access
```

2. **Create a simple `/health/detail` endpoint** (authenticated, Admin+) that returns detailed health info including:
 - Database: connected, migration status
 - Hangfire: server count, job queue depth, failed job count
 - Graph API: token acquisition works
 - File storage: writable
 - Last email poll per company: timestamp, result

3. **Structured Logging Standards:**
 - Ensure all log messages include relevant context:
 - `{CompanyId}`, `{TicketId}`, `{UserId}` where applicable
 - Use Serilog's structured logging, never string interpolation in log messages
 - Log levels:
 - `Information`: user actions (ticket created, status changed)
 - `Warning`: recoverable issues (email attachment skipped, SLA approaching)
 - `Error`: failures that need attention (Graph API failure, job failure)
 - `Debug`: detailed flow for troubleshooting (email matching logic)

---

## Task 7.7 - Documentation

### Instructions

Create the following documents in the `docs/` folder:

1. **`docs/LocalSetup.md`** (update from Phase 1):
 - Prerequisites: .NET 10 SDK, SQL Server, Node.js (if any tooling needs it)
 - Clone, restore, build steps
 - Database setup: connection string, run migrations
 - Azure AD app registration: step-by-step with screenshots/links
 - Graph API permissions: how to grant admin consent
 - Shared mailbox setup: how to create and configure in M365
 - User secrets configuration for local development
 - How to run the app and access Hangfire dashboard

2. **`docs/AzureAdSetup.md`** (update from Phase 1/3):
 - Complete app registration guide
 - App roles configuration
 - Group-to-role mapping
 - API permissions and admin consent
 - Client secret rotation procedures

3. **`docs/DeploymentGuide.md`**:
 - On-premises IIS deployment:
 - Install .NET 10 Hosting Bundle
 - Configure IIS site and app pool
 - Connection string and secrets management (environment variables)
 - SSL certificate setup
 - Firewall rules (port 443 inbound, outbound to Graph API)
 - Database deployment: running migrations in production
 - Hangfire server: considerations for running in the same process vs separate
 - Backup strategy: SQL Server database and file attachment storage
 - Update procedure: zero-downtime approach with staging slot (or brief downtime for v1)

4. **`docs/UserGuide.md`**:
 - For Agents: How to manage tickets, reply, add notes, use KB, view SLA status
 - For Admins: How to manage companies, users, SLA policies, canned responses, KB articles, view reports
 - For SuperAdmins: All admin tasks + audit log, email monitoring, Hangfire dashboard

5. **`docs/Architecture.md`**:
 - Solution structure diagram
 - Data model diagram (entity relationships)
 - Authentication/authorization flow
 - Email processing flow (diagram)
 - SLA monitoring flow (diagram)
 - Technology choices and rationale

---

## Task 7.8 - Final Testing & QA

### Instructions

1. **End-to-End Test Scenarios** (manual QA checklist):

 **Ticket Lifecycle:**
 - [ ] Create ticket via portal -> verify it appears in ticket list
 - [ ] Send email to shared mailbox -> verify ticket is created
 - [ ] Reply to ticket from UI as shared mailbox -> verify customer receives email
 - [ ] Customer replies by email -> verify message appended to ticket
 - [ ] Customer email reopens a closed ticket -> verify status change
 - [ ] Assign ticket -> verify status changes to Open if it was New
 - [ ] Change status through all valid transitions
 - [ ] Upload and download attachments
 - [ ] Add and view internal notes
 - [ ] Insert canned response into reply
 - [ ] Concurrent edit conflict is handled gracefully

 **SLA:**
 - [ ] Configure SLA policy -> verify it appears on tickets
 - [ ] Let SLA countdown approach breach -> verify warning notification
 - [ ] Let SLA breach -> verify breach record and notification
 - [ ] Respond to ticket -> verify FirstResponseAt is set and SLA status updates

 **Satisfaction:**
 - [ ] Close a ticket -> verify survey email is sent
 - [ ] Click rating link -> verify rating page works
 - [ ] Submit rating -> verify it appears on ticket detail
 - [ ] Try expired token -> verify rejection

 **Access Control:**
 - [ ] Agent can only see tickets for assigned companies
 - [ ] Agent cannot access admin pages
 - [ ] Admin can manage their assigned companies but not others
 - [ ] SuperAdmin can access everything
 - [ ] Direct API calls respect the same access rules

 **Reporting:**
 - [ ] Dashboard shows correct counts
 - [ ] Unattended tickets are correctly identified
 - [ ] CSV exports contain correct data
 - [ ] Date range filters work

2. **Performance Baseline:**
 - Measure page load times for: Dashboard, Ticket List (100 tickets), Ticket Detail
 - Measure API response times for key endpoints
 - Document baselines for future comparison

3. **Fix any issues found during QA** before declaring Phase 7 complete.

---

## Acceptance Criteria for Phase 7

- [ ] Audit log captures all data mutations with before/after values
- [ ] Audit log is viewable and searchable by SuperAdmins
- [ ] All pages have loading states, empty states, and error handling
- [ ] No unhandled exceptions visible to users in normal operation
- [ ] Graph API calls have retry/circuit-breaker policies
- [ ] Performance is acceptable (sub-2-second page loads, sub-500ms API responses)
- [ ] Caching is in place for frequently accessed reference data
- [ ] Security headers are set on all responses
- [ ] Rate limiting is active on public endpoints
- [ ] Health check endpoint reports status of all dependencies
- [ ] All documentation is complete and accurate
- [ ] End-to-end QA checklist passes
- [ ] CI/CD pipeline builds, tests, and produces deployable artifacts
- [ ] The system is ready for production use

