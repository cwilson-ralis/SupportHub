# Phase 6 — Audit Reporting & Knowledge Base

> **Prerequisites:** Phases 0–5 complete. Full ticket lifecycle with email, SLA, satisfaction tracking, and routing is operational. Data is flowing for reporting.

---

## Objective

Build SOX-required audit/compliance reporting, an operational performance dashboard, and an internal knowledge base. At the end of this phase, admins can produce SOX audit reports (terminations, new hires, access requests by date range) on demand, agents have a searchable KB at their fingertips, and leadership has clear visibility into support performance.

---

## Task 6.1 — Knowledge Base Service

### Instructions

1. **Create `IKnowledgeBaseService`** in `Core/Interfaces/`:

```csharp
public interface IKnowledgeBaseService
{
    Task<Result<KbArticleDto>> CreateAsync(CreateKbArticleDto dto);
    Task<Result<KbArticleDto>> UpdateAsync(int id, UpdateKbArticleDto dto);
    Task<Result<KbArticleDto>> GetByIdAsync(int id);
    Task<Result<PagedResult<KbArticleListDto>>> SearchAsync(KbSearchDto searchDto);
    Task<Result<bool>> TogglePublishAsync(int id);
    Task<Result<bool>> SoftDeleteAsync(int id);
}
```

2. **Create DTOs:**

   - **`KbArticleDto`** (full): Id, CompanyId, CompanyName, AuthorName, Title, Body (Markdown), BodyHtml (rendered), Tags (list), IsPublished, CreatedAt, UpdatedAt
   - **`KbArticleListDto`** (grid row): Id, Title, CompanyName, AuthorName, Tags, IsPublished, CreatedAt, UpdatedAt
   - **`CreateKbArticleDto`**: CompanyId, Title, Body (Markdown), Tags (comma-separated string)
   - **`UpdateKbArticleDto`**: Title, Body, Tags
   - **`KbSearchDto`**: CompanyId?, SearchTerm?, Tags?, IsPublished?, PageNumber, PageSize

3. **Implement `KnowledgeBaseService`:**

   - Articles are scoped by company — agents only see articles for their assigned companies
   - SuperAdmins see all
   - `SearchAsync` performs a SQL `LIKE` search across Title, Body, and Tags for v1 (can be upgraded to full-text search later)
   - Render Markdown to HTML using the `Markdig` NuGet package — store only Markdown in the database, render on read
   - Only Admin/SuperAdmin can create/edit/delete articles; Agents have read-only access

4. **Install `Markdig`** in the Infrastructure project:

```bash
dotnet add src/SupportHub.Infrastructure/SupportHub.Infrastructure.csproj package Markdig
```

---

## Task 6.2 — Knowledge Base Blazor Pages

### Instructions

1. **Create `Pages/KnowledgeBase/KbArticleList.razor`:**

   - Top bar: Company selector, Search text box, Tag filter chips, "New Article" button (Admin+)
   - Grid/card view toggle:
     - **Grid view**: `MudDataGrid` with columns: Title (link), Company, Author, Tags (as chips), Published (toggle icon), Last Updated
     - **Card view**: `MudCard` tiles showing title, first 150 chars of body (plain text preview), tags, author, date
   - Default: show only published articles; Admin+ can toggle to see drafts
   - Pagination

2. **Create `Pages/KnowledgeBase/KbArticleDetail.razor`:**

   - **View mode** (default for Agents):
     - Title
     - Metadata bar: Company, Author, Last Updated, Tags as chips
     - Body rendered as HTML from Markdown (use `Markdig` output, sanitized)
     - "Copy Link" button

   - **Edit mode** (Admin+ only):
     - Title input
     - Tags input (comma-separated or chip-based input)
     - Side-by-side Markdown editor and live preview:
       - Left: `MudTextField` multiline for Markdown
       - Right: Rendered HTML preview (re-render on debounced input, ~500ms delay)
     - Published toggle
     - Save / Cancel buttons
     - Delete button with confirmation dialog

3. **Create `Pages/KnowledgeBase/KbArticleCreate.razor`:**
   - Same layout as edit mode
   - Company selector (required)
   - Pre-fill author from current user

---

## Task 6.3 — KB Integration in Ticket Detail

### Instructions

1. **Add a "Knowledge Base" panel/tab** to the Ticket Detail page:

   This can be implemented as either:
   - A collapsible side panel (next to the properties panel), OR
   - A tab alongside the conversation timeline

   **Recommended: Collapsible side panel triggered by a "Search KB" button in the reply composer area.**

   **Behavior:**
   - When the agent clicks "Search KB", a panel slides open
   - It shows a search box and results filtered to the ticket's company
   - The search box auto-fills with the ticket subject as a starting query
   - Results show: article title, first 100 chars of body, relevance
   - Clicking an article opens a read-only preview within the panel
   - "Insert Link" button: inserts a Markdown-formatted link to the article into the reply composer
   - "Copy Content" button: copies the article body (rendered as plain text) to clipboard for the agent to paste/adapt

2. **Add "Suggested Articles"** to the ticket detail page:
   - Automatically search KB using keywords from the ticket subject
   - Show the top 3 matching articles in the properties panel under a "Suggested Articles" section
   - Only show if there are results with reasonable relevance (at least one keyword match)

---

## Task 6.4 — SOX Compliance Reporting Service

### Instructions

SOX audit support is a **required feature** explicitly identified during kickoff. Auditors must be able to retrieve ticket histories filtered by category and date range without requiring direct database access.

1. **Create `IComplianceReportingService`** in `Core/Interfaces/`:

```csharp
public interface IComplianceReportingService
{
    Task<ComplianceReportDto> GetTaggedTicketsReportAsync(ComplianceReportFilterDto filter);
    Task<byte[]> ExportTaggedTicketsToCsvAsync(ComplianceReportFilterDto filter);
}

public record ComplianceReportFilterDto(
    string Tag,                 // e.g., "termination", "new-hire", "access-request"
    DateTimeOffset DateFrom,
    DateTimeOffset DateTo,
    int? CompanyId = null);     // null = all companies (SuperAdmin)

public record ComplianceReportDto(
    string Tag,
    DateTimeOffset DateFrom,
    DateTimeOffset DateTo,
    int TotalCount,
    List<ComplianceTicketRowDto> Tickets);

public record ComplianceTicketRowDto(
    int TicketId,
    string Subject,
    string CompanyName,
    string RequesterName,
    string RequesterEmail,
    string AssignedAgentName,
    TicketStatus Status,
    IssueType? IssueType,
    List<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt);
```

2. **Implement `ComplianceReportingService`:**
   - Query tickets that have a `TicketTag` matching the given tag, filtered by date range (on `CreatedAt`)
   - Include soft-deleted tickets (the compliance report must show retained data — use `.IgnoreQueryFilters()`)
   - Apply company access check: SuperAdmin sees all companies; Admin sees assigned companies only
   - Sort by `CreatedAt` descending by default
   - For CSV export: use `CsvHelper`, UTF-8 with BOM, all key fields, dates in `yyyy-MM-dd HH:mm:ss UTC` format

3. **Create Blazor page `Pages/Reports/ComplianceReport.razor`:**

   **Layout:**
   - Tag selector (dropdown with well-known tags: Termination, New Hire, Access Request, + free-text for custom tags)
   - Company filter (dropdown, or "All Companies" for SuperAdmin)
   - Date range picker (required)
   - "Run Report" button
   - Results section (hidden until report is run):
     - Summary: "X tickets tagged `{tag}` between {from} and {to}"
     - `MudDataGrid` with columns: Ticket #, Subject, Company, Requester, Agent, Status, Issue Type, Created, Resolved/Closed
     - Each row links to ticket detail
     - "Export to CSV" button
   - Accessible to Admin/SuperAdmin only

4. **Add well-known compliance tags** as constants in `Core/Constants/`:

```csharp
public static class WellKnownTags
{
    public const string Termination = "termination";
    public const string NewHire = "new-hire";
    public const string AccessRequest = "access-request";
    public const string Empower = "empower";
    public const string Salesforce = "salesforce";
}
```

5. **Unit tests:**
   - Test: report filters correctly by tag and date range
   - Test: soft-deleted tickets are included in compliance reports
   - Test: company access is enforced (agent cannot see other company's compliance data)
   - Test: CSV export includes all required fields

---

## Task 6.5 — Operational Reporting Service

### Instructions

1. **Create `IReportingService`** in `Core/Interfaces/`:

```csharp
public interface IReportingService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(ReportFilterDto filter);
    Task<TicketVolumeReportDto> GetTicketVolumeAsync(ReportFilterDto filter);
    Task<SlaPerformanceReportDto> GetSlaPerformanceAsync(ReportFilterDto filter);
    Task<AgentPerformanceReportDto> GetAgentPerformanceAsync(ReportFilterDto filter);
    Task<UnattendedTicketsReportDto> GetUnattendedTicketsAsync(ReportFilterDto filter);
}
```

2. **Create report DTOs:**

   **`ReportFilterDto`:**
   - CompanyId? (null = all accessible companies)
   - DateFrom (required)
   - DateTo (required)
   - AgentId? (optional)
   - Priority? (optional)

   **`DashboardSummaryDto`:**
   - TotalTicketsCreated (in period)
   - TotalTicketsResolved
   - TotalTicketsClosed
   - CurrentOpenTickets (snapshot)
   - CurrentUnassignedTickets (snapshot)
   - AverageFirstResponseMinutes
   - AverageResolutionMinutes
   - SlaBreachCount
   - SlaBreachRate (percentage)
   - AverageSatisfactionScore
   - SatisfactionResponseRate (percentage)
   - TicketsCreatedTrend (compared to previous period: up/down/flat + percentage)

   **`TicketVolumeReportDto`:**
   - DailyVolumes: List of { Date, Created, Resolved, Closed }
   - ByStatus: List of { Status, Count }
   - ByPriority: List of { Priority, Count }
   - BySource: List of { Source, Count }
   - ByCompany: List of { CompanyName, Count }

   **`SlaPerformanceReportDto`:**
   - OverallFirstResponseRate (percentage met)
   - OverallResolutionRate (percentage met)
   - ByPriority: List of { Priority, FirstResponseRate, ResolutionRate, AvgFirstResponseMin, AvgResolutionMin }
   - ByCompany: List of { CompanyName, FirstResponseRate, ResolutionRate }
   - BreachDetails: List of { TicketId, Subject, Company, BreachType, BreachedAt, ExceededByMinutes }

   **`AgentPerformanceReportDto`:**
   - Agents: List of { AgentName, TicketsHandled, TicketsResolved, AvgFirstResponseMin, AvgResolutionMin, SlaBreachCount, AvgSatisfactionScore }

   **`UnattendedTicketsReportDto`** (the "are we dropping tickets?" report):
   - UnassignedTickets: List of { TicketId, Subject, Company, Priority, CreatedAt, AgeMinutes, SlaUrgency }
   - NoResponseTickets: List of { TicketId, Subject, Company, AssignedAgent, CreatedAt, AgeMinutes, SlaUrgency } — tickets with no outbound message
   - StaleTickets: List of { TicketId, Subject, Company, AssignedAgent, LastActivityAt, DaysSinceActivity, Status } — open tickets with no activity in X days (configurable, default 3)
   - TotalUnassigned, TotalNoResponse, TotalStale (counts)

3. **Implement `ReportingService`:**
   - All queries respect company access (user's assigned companies, SuperAdmin sees all)
   - Use EF Core projections (`.Select()`) for efficiency — do NOT load full entities
   - For date-range aggregations, group by date in the query
   - For the "previous period" comparison in DashboardSummary: if the filter is 30 days, compare to the prior 30 days
   - Stale ticket threshold: configurable via `ReportingSettings.StaleTicketDays` (default 3)

---

## Task 6.6 — Dashboard Page

### Instructions

1. **Create `Pages/Dashboard/Dashboard.razor`:**

   This is the landing page after login.

   **Top bar:**
   - Company filter dropdown (or "All Companies")
   - Date range selector: Quick picks (Today, Last 7 Days, Last 30 Days, Custom) + custom date range picker

   **Summary cards row (use `MudPaper` or `MudCard`):**
   ```
   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
   │ Open Tickets  │ │ Unassigned   │ │ SLA Breaches │ │ Avg Response │ │ Satisfaction │
   │     42        │ │     7 ⚠      │ │    3 🔴      │ │   2.5 hrs    │ │   4.2 ★      │
   │   ↑ 12%      │ │              │ │   (8% rate)  │ │   ↓ from 3h  │ │   85% resp   │
   └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
   ```
   - "Unassigned" card should be amber/red if count > 0
   - Trend arrows show comparison to previous period

   **Charts row (use MudBlazor chart components or a lightweight charting library):**
   - **Ticket Volume Over Time**: Line chart showing tickets created vs resolved per day
   - **Tickets by Status**: Donut/pie chart of current open tickets by status
   - **Tickets by Priority**: Bar chart

   **Attention Required section:**
   - **Unassigned Tickets**: Table of tickets with no assigned agent, sorted by age (oldest first)
   - **Awaiting Response**: Table of tickets with no outbound message, sorted by SLA urgency
   - **Stale Tickets**: Table of open tickets with no activity in 3+ days
   - Each table row is clickable and navigates to the ticket detail
   - Show max 10 rows with "View All" link to a filtered ticket list

2. **Auto-refresh:** Refresh dashboard data every 5 minutes (simple `Timer` in the Blazor component).

---

## Task 6.7 — Reports Page

### Instructions

1. **Create `Pages/Reports/Reports.razor`:**

   **Tabbed layout with the following report tabs:**

   **Tab 1: SLA Performance**
   - Summary cards: Overall First Response Rate, Overall Resolution Rate
   - Table: SLA performance by priority (First Response %, Resolution %, Avg Times)
   - Table: SLA performance by company
   - Table: Recent breaches (last 20) with links to tickets

   **Tab 2: Agent Performance**
   - Table: One row per agent with metrics (Tickets Handled, Resolved, Avg Response Time, Avg Resolution Time, Breach Count, Avg Satisfaction Score)
   - Sortable by any column

   **Tab 3: Ticket Volume**
   - Line chart: daily ticket volume (created, resolved, closed) over the selected period
   - Breakdown tables: by status, by priority, by source, by company

   **Tab 4: Unattended Tickets** (the "dropped tickets" report)
   - Three sections: Unassigned, No Response, Stale
   - Each is a sortable table with direct links to tickets
   - Summary counts at the top
   - This tab should have a visual warning if any counts are > 0

   **Tab 5: Compliance / Audit** (SOX support — Admin/SuperAdmin only)
   - Links to the `ComplianceReport` page (Task 6.4) for each well-known tag:
     - "View Termination Report"
     - "View New Hire Report"
     - "View Access Request Report"
   - Or embed the compliance report form inline on this tab
   - Reminder: "These reports include soft-deleted tickets to satisfy data retention requirements."

2. **All tabs share:**
   - Company filter and date range filter (same as dashboard)
   - "Export to CSV" button per table (generate CSV on the server, download via file endpoint)

---

## Task 6.8 — CSV Export

### Instructions

1. **Create `IExportService`** in `Core/Interfaces/`:

```csharp
public interface IExportService
{
    Task<byte[]> ExportToCsvAsync<T>(List<T> data, string[] columnHeaders);
    Task<byte[]> ExportTicketsToCsvAsync(TicketFilterDto filter);
    Task<byte[]> ExportSlaBreachesToCsvAsync(ReportFilterDto filter);
}
```

2. **Implement using `CsvHelper` NuGet package:**
   - Generate clean CSVs with headers
   - Format dates as `yyyy-MM-dd HH:mm:ss`
   - Format durations as "Xh Ym"
   - UTF-8 encoding with BOM (for Excel compatibility)

3. **Add export endpoints to the API:**

```
GET /api/v1/reports/tickets/export?{filters}      → CSV of ticket list
GET /api/v1/reports/sla-breaches/export?{filters}  → CSV of SLA breaches
GET /api/v1/reports/agents/export?{filters}        → CSV of agent performance
```

4. **In the Blazor UI**, export buttons trigger a JS interop file download.

---

## Task 6.9 — KB API Endpoints

### Instructions

```
GET    /api/v1/knowledge-base?{searchDto}                → Search articles
GET    /api/v1/knowledge-base/{id}                        → Get article
POST   /api/v1/knowledge-base                             → Create (AdminOrAbove)
PUT    /api/v1/knowledge-base/{id}                        → Update (AdminOrAbove)
PATCH  /api/v1/knowledge-base/{id}/publish                → Toggle publish (AdminOrAbove)
DELETE /api/v1/knowledge-base/{id}                        → Soft delete (AdminOrAbove)

GET    /api/v1/reports/dashboard?{filter}                 → Dashboard summary
GET    /api/v1/reports/ticket-volume?{filter}             → Ticket volume report
GET    /api/v1/reports/sla-performance?{filter}           → SLA performance report
GET    /api/v1/reports/agent-performance?{filter}         → Agent performance report
GET    /api/v1/reports/unattended?{filter}                → Unattended tickets report

GET    /api/v1/reports/compliance?tag={tag}&from={date}&to={date}&companyId={id}  → Compliance/SOX report
GET    /api/v1/reports/compliance/export?{same filters}   → CSV export of compliance report
```

---

## Task 6.10 — Navigation Updates

### Instructions

1. **Update sidebar navigation** (from Phase 2, Task 2.10):
   - **Dashboard** → now links to the real dashboard (Phase 6)
   - **Tickets** → existing ticket list
   - **Knowledge Base** → now active, links to KB article list
   - **Admin** section:
     - Companies
     - Users
     - Divisions / Queues (active from Phase 4)
     - Routing Rules (active from Phase 4)
     - Canned Responses
     - SLA Policies (active from Phase 5)
   - **Reports** → links to the reports page (Admin/SuperAdmin only)

2. **Set Dashboard as the default landing page** after login.

---

## Task 6.11 — Testing

### Instructions

1. **Unit tests for `KnowledgeBaseService`:**
   - Test: create article, verify Markdown body is stored
   - Test: search by keyword matches title and body
   - Test: company access is enforced
   - Test: publish toggle works

2. **Unit tests for `ReportingService`:**
   - Test: dashboard summary with known data produces correct counts
   - Test: SLA performance rates are calculated correctly
   - Test: unattended tickets correctly identifies stale, unassigned, and no-response tickets
   - Test: date range filtering works
   - Test: company access filtering works

3. **Unit tests for `ComplianceReportingService`:**
   - Test: report returns tickets with matching tag in date range
   - Test: report includes soft-deleted tickets
   - Test: report excludes tickets outside date range
   - Test: company access enforced (agent cannot see other company's data)
   - Test: CSV export contains all required columns

4. **Unit tests for `ExportService`:**
   - Test: CSV output has correct headers and formatting

---

## Acceptance Criteria for Phase 6

- [ ] Agents can browse, search, and read KB articles filtered by company
- [ ] Admins can create, edit, publish/unpublish, and delete KB articles
- [ ] Markdown editor with live preview works correctly
- [ ] KB search panel in ticket detail helps agents find relevant articles while replying
- [ ] Suggested articles appear on ticket detail based on ticket subject
- [ ] Dashboard shows summary cards, charts, and "Attention Required" sections
- [ ] Dashboard correctly identifies unassigned, no-response, and stale tickets
- [ ] Reports page shows SLA performance, agent performance, ticket volume, and unattended tickets
- [ ] All report tables are sortable and filterable by company and date range
- [ ] CSV export works for ticket lists, SLA breaches, and agent performance
- [ ] Company-level access control is enforced across all reporting
- [ ] Dashboard is the default landing page after login
- [ ] SOX compliance report returns all tickets with a given tag (e.g., `termination`) filtered by date range
- [ ] SOX compliance report includes soft-deleted (retained) tickets
- [ ] SOX compliance report is exportable to CSV for submission to auditors
- [ ] Well-known compliance tags (`termination`, `new-hire`, `access-request`) have dedicated report shortcuts
- [ ] All new services have unit tests
