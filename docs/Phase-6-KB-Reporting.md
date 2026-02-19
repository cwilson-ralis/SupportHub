# Phase 6 — Knowledge Base & Reporting

## Overview
Audit/SOX-oriented reports, dashboard with key metrics, CSV export, knowledge base article CRUD with search. This phase surfaces the data accumulated in Phases 1-5 and adds self-service knowledge content.

## Prerequisites
- Phase 1-5 complete (all entities, audit log, SLA, CSAT data)

## Wave 1 — Domain Entities

### KnowledgeBaseArticle Entity
- Id (Guid, PK)
- CompanyId (Guid?, FK→Company, nullable — null means global/cross-company)
- Title (string, required, max 500)
- Slug (string, required, max 500, unique)
- Content (string, required — Markdown body)
- Summary (string?, max 1000 — short description for search results)
- Category (string?, max 200)
- Tags (string?, max 1000 — comma-separated)
- AuthorId (Guid, FK→ApplicationUser, required)
- PublishedAt (DateTimeOffset?)
- IsPublished (bool, default false)
- ViewCount (int, default 0)
- SortOrder (int, default 0)
- Plus BaseEntity fields
- Navigations: Company, Author

### ArticleStatus (no enum needed — use IsPublished bool + PublishedAt)

## Wave 2 — EF Configuration & Migration

### KnowledgeBaseArticleConfiguration
- Table: KnowledgeBaseArticles
- PK: Id
- FK: CompanyId → Companies (SetNull), AuthorId → ApplicationUsers (Restrict)
- Unique index: Slug
- Indexes: CompanyId, Category, IsPublished, AuthorId, Tags (for LIKE queries)
- Full-text index on Title + Content + Summary (SQL Server FTS if available, or fallback to LIKE)
- MaxLength: Title 500, Slug 500, Summary 1000, Category 200, Tags 1000

### Migration
`dotnet ef migrations add AddKnowledgeBase`

Note: No schema changes for reporting — reports query existing AuditLogEntry, Ticket, SlaBreachRecord, CustomerSatisfactionRating tables.

## Wave 3 — Service Interfaces & DTOs

### Knowledge Base DTOs
```csharp
public record KnowledgeBaseArticleDto(Guid Id, Guid? CompanyId, string? CompanyName, string Title, string Slug, string Content, string? Summary, string? Category, IReadOnlyList<string> Tags, Guid AuthorId, string AuthorName, DateTimeOffset? PublishedAt, bool IsPublished, int ViewCount, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public record KnowledgeBaseArticleSummaryDto(Guid Id, string Title, string Slug, string? Summary, string? Category, IReadOnlyList<string> Tags, bool IsPublished, int ViewCount, DateTimeOffset? PublishedAt);
public record CreateArticleRequest(Guid? CompanyId, string Title, string Content, string? Summary, string? Category, IReadOnlyList<string>? Tags);
public record UpdateArticleRequest(string Title, string Content, string? Summary, string? Category, IReadOnlyList<string>? Tags, bool IsPublished);
public record ArticleSearchRequest(string? SearchTerm, Guid? CompanyId, string? Category, bool? IsPublished, int Page, int PageSize);
```

### Reporting DTOs
```csharp
public record DashboardMetricsDto(
    int TotalOpenTickets,
    int TicketsCreatedToday,
    int TicketsResolvedToday,
    int TicketsClosedToday,
    double AverageResolutionTimeHours,
    double AverageFirstResponseTimeHours,
    int ActiveSlaBreaches,
    double CsatAverageRating,
    IReadOnlyList<TicketsByStatusDto> TicketsByStatus,
    IReadOnlyList<TicketsByPriorityDto> TicketsByPriority,
    IReadOnlyList<TicketsByCompanyDto> TicketsByCompany,
    IReadOnlyList<TicketTrendDto> TicketTrend
);

public record TicketsByStatusDto(string Status, int Count);
public record TicketsByPriorityDto(string Priority, int Count);
public record TicketsByCompanyDto(string CompanyName, int OpenCount, int ClosedCount);
public record TicketTrendDto(DateTimeOffset Date, int Created, int Resolved, int Closed);

public record AuditReportRequest(Guid? CompanyId, string? EntityType, string? Action, string? UserId, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize);
public record AuditReportEntryDto(Guid Id, DateTimeOffset Timestamp, string? UserDisplayName, string Action, string EntityType, string EntityId, string? OldValues, string? NewValues);

public record TicketReportRequest(Guid? CompanyId, TicketStatus? Status, TicketPriority? Priority, Guid? QueueId, IReadOnlyList<string>? Tags, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize);
public record TicketReportRowDto(string TicketNumber, string Subject, string CompanyName, string Status, string Priority, string? QueueName, string? AssignedAgent, string RequesterEmail, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, DateTimeOffset? ClosedAt, double? ResolutionTimeHours, IReadOnlyList<string> Tags);
```

### IKnowledgeBaseService
```csharp
public interface IKnowledgeBaseService
{
    Task<Result<PagedResult<KnowledgeBaseArticleSummaryDto>>> SearchArticlesAsync(ArticleSearchRequest request, CancellationToken ct = default);
    Task<Result<KnowledgeBaseArticleDto>> GetArticleByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<KnowledgeBaseArticleDto>> GetArticleBySlugAsync(string slug, CancellationToken ct = default);
    Task<Result<KnowledgeBaseArticleDto>> CreateArticleAsync(CreateArticleRequest request, CancellationToken ct = default);
    Task<Result<KnowledgeBaseArticleDto>> UpdateArticleAsync(Guid id, UpdateArticleRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteArticleAsync(Guid id, CancellationToken ct = default);
    Task<Result<bool>> IncrementViewCountAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> GetCategoriesAsync(Guid? companyId, CancellationToken ct = default);
}
```

### IDashboardService
```csharp
public interface IDashboardService
{
    Task<Result<DashboardMetricsDto>> GetDashboardMetricsAsync(Guid? companyId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}
```

### IReportService
```csharp
public interface IReportService
{
    Task<Result<PagedResult<AuditReportEntryDto>>> GetAuditReportAsync(AuditReportRequest request, CancellationToken ct = default);
    Task<Result<PagedResult<TicketReportRowDto>>> GetTicketReportAsync(TicketReportRequest request, CancellationToken ct = default);
    Task<Result<byte[]>> ExportAuditReportCsvAsync(AuditReportRequest request, CancellationToken ct = default);
    Task<Result<byte[]>> ExportTicketReportCsvAsync(TicketReportRequest request, CancellationToken ct = default);
}
```

## Wave 4 — Service Implementations

### KnowledgeBaseService
- Slug generation from title (lowercase, hyphens, strip special chars)
- Duplicate slug handling (append -2, -3 etc.)
- Full-text search: SQL Server CONTAINS or LIKE fallback on Title + Content + Summary
- Company isolation: show global articles + articles for user's accessible companies
- Category listing for filters
- View count increment (fire-and-forget, don't block)
- Audit logging on create/update/delete/publish

### DashboardService
- Aggregate queries with company isolation
- TicketsByStatus: GROUP BY Status, COUNT
- TicketsByPriority: GROUP BY Priority, COUNT
- TicketsByCompany: GROUP BY Company, pivot Open/Closed
- TicketTrend: GROUP BY DATE(CreatedAt) for last 30 days
- Average resolution time: AVG(ResolvedAt - CreatedAt) in hours
- Average first response time: AVG(FirstResponseAt - CreatedAt) in hours
- Active SLA breaches: count of unacknowledged SlaBreachRecords
- CSAT average: AVG(Rating)
- All filtered by user's accessible companies

### ReportService
- AuditReport: query AuditLogEntry with filters, ordered by Timestamp desc
- TicketReport: join Ticket + Company + Queue + Agent, with filters, ordered by CreatedAt desc
- CSV export: generate CSV byte array using StringBuilder or CsvHelper
  - Include BOM for Excel compatibility
  - Proper escaping of commas, quotes, newlines
  - Date formatting in ISO 8601

## Wave 5 — Blazor UI Pages

### Dashboard Page (`/`) — Replace Phase 1 placeholder
- MudGrid layout with metric cards:
  - Open Tickets (MudPaper with big number)
  - Created Today / Resolved Today / Closed Today
  - Avg First Response Time / Avg Resolution Time
  - CSAT Average Rating (star display)
  - Active SLA Breaches (red if > 0)
- Charts (MudChart or integrate a charting library):
  - Ticket trend line chart (last 30 days: created vs resolved)
  - Tickets by status (donut/pie chart)
  - Tickets by priority (bar chart)
  - Tickets by company (horizontal bar chart)
- Company filter at top
- Date range picker for trend data
- Auto-refresh every 5 minutes

### Audit Report Page (`/reports/audit`)
- Filters: company, entity type, action, user, date range
- MudDataGrid with server-side pagination
- Columns: Timestamp, User, Action, Entity Type, Entity ID, expandable row for old/new values
- Export to CSV button

### Ticket Report Page (`/reports/tickets`)
- Filters: company, status, priority, queue, tags, date range
- MudDataGrid with server-side pagination
- Columns: Ticket#, Subject, Company, Status, Priority, Queue, Agent, Requester, Created, Resolved, Closed, Resolution Time, Tags
- Export to CSV button
- SOX-focused preset filters (e.g., "Terminations last 30 days", "Access requests this quarter")

### Knowledge Base List (`/kb`)
- Search bar with autocomplete
- Category sidebar/filter chips
- Article cards with title, summary, category, tags, view count
- Grid/list toggle
- Company filter for admins

### Knowledge Base Article View (`/kb/{slug}`)
- Full article display with Markdown rendering
- Sidebar: category, tags, author, published date, view count
- Related articles (same category/tags)
- "Was this helpful?" feedback (optional)

### Knowledge Base Admin (`/admin/kb`)
- MudDataGrid: title, category, status (draft/published), author, views, dates
- Create/Edit page with:
  - Title, category selector/input, tags input
  - Markdown editor (MudTextField multiline or integrate a markdown editor component)
  - Preview toggle
  - Publish/unpublish action
  - Company scope selector (global or specific company)

## Wave 6 — API Controllers

### DashboardController
- GET /api/dashboard?companyId={id}&from={date}&to={date} — metrics

### ReportsController
- GET /api/reports/audit — audit report with filters
- GET /api/reports/audit/export — CSV export
- GET /api/reports/tickets — ticket report with filters
- GET /api/reports/tickets/export — CSV export

### KnowledgeBaseController
- GET /api/kb — search/list articles
- GET /api/kb/{id} — article by ID
- GET /api/kb/slug/{slug} — article by slug
- POST /api/kb — create article
- PUT /api/kb/{id} — update article
- DELETE /api/kb/{id} — soft-delete
- GET /api/kb/categories — list categories
- POST /api/kb/{id}/view — increment view count

## Wave 7 — Tests

### Unit Tests
- KnowledgeBaseServiceTests: CRUD, slug generation, duplicate slug handling, search, company isolation, view count, publish/unpublish
- DashboardServiceTests: metrics aggregation, company filtering, date range filtering, empty data handling
- ReportServiceTests: audit report filtering/pagination, ticket report filtering/pagination, CSV export format validation
- CSV export tests: verify headers, proper escaping, date formatting, BOM present

## Acceptance Criteria
- [ ] KnowledgeBaseArticle entity with proper EF configuration
- [ ] Migration runs successfully
- [ ] Dashboard displays all metric cards and charts
- [ ] Dashboard respects company filter
- [ ] Audit report filters and paginates correctly
- [ ] Ticket report filters and paginates correctly
- [ ] CSV exports open correctly in Excel
- [ ] KB articles support full Markdown
- [ ] KB search works (full-text or LIKE)
- [ ] KB articles scoped by company
- [ ] KB slug generation works with duplicates
- [ ] SOX preset filters work (terminations, access requests by date range)
- [ ] All unit tests pass
- [ ] `dotnet build` — zero errors

## Dependencies
- Phase 1: AuditLogEntry, Company, ApplicationUser
- Phase 2: Ticket, TicketTag
- Phase 4: Queue
- Phase 5: SlaBreachRecord, CustomerSatisfactionRating

## Next Phase
Phase 7 (Hardening) focuses on audit validation, security review, performance optimization, and production readiness.
