# Phase 7 — Hardening & Production Readiness

## Overview
Security review, audit log validation, performance optimization, health checks, structured logging, accessibility, SignalR real-time updates, API documentation, and integration tests. This phase ensures the system is production-ready.

## Prerequisites
- Phases 1-6 complete (all features implemented)

## Wave 1 — Audit & Security Validation

### Audit Log Completeness Review
- Verify every CUD (Create, Update, Delete) operation across all services writes to AuditLogEntry
- Create a checklist of all service methods and their expected audit entries:
  - CompanyService: Create, Update, Delete
  - UserService: SyncUser, AssignRole, RemoveRole
  - TicketService: Create, Update, Assign, ChangeStatus, ChangePriority, Delete
  - TicketMessageService: AddMessage
  - InternalNoteService: AddNote
  - AttachmentService: Upload, Delete
  - CannedResponseService: Create, Update, Delete
  - TagService: Add, Remove
  - EmailConfigurationService: Create, Update, Delete
  - QueueService: Create, Update, Delete
  - RoutingRuleService: Create, Update, Delete, Reorder
  - SlaPolicyService: Create, Update, Delete
  - SlaMonitoringService: AcknowledgeBreach
  - CustomerSatisfactionService: SubmitRating
  - KnowledgeBaseService: Create, Update, Delete, Publish, Unpublish
- Write integration tests that verify audit entries for each operation

### Company Isolation Audit
- Review every service query to verify CompanyId filtering
- Verify that ICurrentUserService.HasAccessToCompanyAsync is called or company filter is applied
- Services to verify:
  - TicketService: all queries filter by user's accessible companies
  - TicketMessageService: access through ticket company check
  - InternalNoteService: access through ticket company check
  - AttachmentService: access through ticket company check
  - CannedResponseService: global + user's companies
  - QueueService: company isolation
  - RoutingRuleService: company isolation
  - SlaPolicyService: company isolation
  - KnowledgeBaseService: global + user's companies
  - ReportService: company isolation
  - DashboardService: company isolation
- Write integration tests that verify cross-company access is denied

### Authorization Review
- Verify all API endpoints have proper [Authorize] attributes with correct policies
- Verify Blazor pages have proper @attribute [Authorize] directives
- Test: unauthenticated access returns 401
- Test: unauthorized role access returns 403
- Test: SuperAdmin can access all company data
- Test: Admin can only access their assigned companies
- Test: Agent can only access their assigned companies

### Input Validation Review
- Verify all user inputs are validated (max length, required, format)
- Verify file uploads check: file size limits, allowed extensions, content type validation
- Verify no SQL injection vectors (parameterized queries via EF)
- Verify no XSS vectors (Blazor auto-escapes, but review raw HTML rendering)
- Verify email addresses are validated
- Verify tag values are sanitized

## Wave 2 — Performance & Database Optimization

### Database Indexes
Review and add missing indexes based on query patterns:
- Tickets: CompanyId + Status (composite), CompanyId + AssignedAgentId, CompanyId + CreatedAt, CompanyId + Priority
- TicketMessages: TicketId + CreatedAt
- TicketTags: Tag (for popular tags query), CompanyId via ticket join
- AuditLogEntry: Timestamp + EntityType (composite for report queries)
- SlaBreachRecords: BreachedAt + AcknowledgedAt (for active breaches query)
- KnowledgeBaseArticles: IsPublished + CompanyId (composite for published article queries)

### Query Optimization
- Review all service methods for N+1 query issues
- Add .Include() / .ThenInclude() where needed to prevent lazy loading traps
- Use .AsNoTracking() for read-only queries
- Use projection (Select) instead of loading full entities where only DTOs are needed
- Verify pagination is done at the database level (Skip/Take) not in memory

### EF Migration: Performance Indexes
`dotnet ef migrations add AddPerformanceIndexes`

### Caching Strategy
- Add IMemoryCache for frequently-read, rarely-changed data:
  - Company list (cache 5 min)
  - Queue list per company (cache 5 min)
  - Canned responses per company (cache 5 min)
  - Popular tags (cache 10 min)
  - SLA policies per company (cache 5 min)
- Cache invalidation on write operations

### Connection Resiliency
- Configure EF Core retry policy for transient SQL errors
- Configure Hangfire retry policy for failed jobs
- Configure Graph API retry policy with exponential backoff

## Wave 3 — Observability & Health Checks

### Serilog Configuration
```csharp
// Program.cs
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/supporthub-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));
```

### Structured Logging Standards
- All services use `ILogger<T>` (already convention)
- Log at appropriate levels:
  - Information: successful operations, job completions
  - Warning: SLA breaches, email processing skips, validation failures
  - Error: unhandled exceptions, external service failures
  - Debug: query details, routing engine evaluation steps
- Include structured properties: CompanyId, TicketId, UserId in log scopes
- Example:
```csharp
using (_logger.BeginScope(new Dictionary<string, object> { ["CompanyId"] = companyId, ["TicketId"] = ticketId }))
{
    _logger.LogInformation("Ticket {TicketNumber} assigned to agent {AgentId}", ticket.TicketNumber, agentId);
}
```

### Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddHangfire(options => { options.MinimumAvailableServers = 1; }, name: "hangfire")
    .AddCheck<GraphApiHealthCheck>("graph-api")
    .AddCheck<FileStorageHealthCheck>("file-storage");
```

Custom health checks:
- GraphApiHealthCheck: verify Graph API token acquisition works
- FileStorageHealthCheck: verify storage path is accessible and writable

Map health endpoints:
- `/health` — overall health
- `/health/ready` — readiness (all dependencies)
- `/health/live` — liveness (app is running)

### Request Logging Middleware
- Log all API requests with: method, path, status code, duration, user ID
- Exclude health check endpoints from logging
- Use Serilog.AspNetCore RequestLogging

## Wave 4 — SignalR Real-Time Updates

### Hub Design
```csharp
public class TicketHub : Hub
{
    // Client joins groups based on their company access
    public async Task JoinCompanyGroup(Guid companyId) { ... }
    public async Task LeaveCompanyGroup(Guid companyId) { ... }
    public async Task JoinTicketGroup(Guid ticketId) { ... }
    public async Task LeaveTicketGroup(Guid ticketId) { ... }
}
```

### Real-Time Events
- TicketCreated: notify company group
- TicketUpdated: notify company group + ticket group
- TicketAssigned: notify company group + ticket group + assigned agent
- NewMessage: notify ticket group
- NewInternalNote: notify ticket group (agents only)
- SlaBreachDetected: notify company group (admins/agents)

### INotificationService (Application)
```csharp
public interface INotificationService
{
    Task NotifyTicketCreatedAsync(Guid companyId, TicketSummaryDto ticket, CancellationToken ct = default);
    Task NotifyTicketUpdatedAsync(Guid companyId, Guid ticketId, string changeDescription, CancellationToken ct = default);
    Task NotifyNewMessageAsync(Guid ticketId, TicketMessageDto message, CancellationToken ct = default);
    Task NotifySlaBreachAsync(Guid companyId, SlaBreachRecordDto breach, CancellationToken ct = default);
}
```

### UI Integration
- Ticket list: auto-refresh when TicketCreated/TicketUpdated received for current company
- Ticket detail: auto-append new messages/notes when received
- Toast notifications for assignments and SLA breaches
- Connection status indicator in layout

## Wave 5 — API Documentation & Accessibility

### Swagger/OpenAPI
- Add Swashbuckle.AspNetCore
- Configure XML comments for API docs
- Group endpoints by controller/feature
- Add authentication scheme (Bearer token) to Swagger UI
- Endpoint: `/swagger`

### Accessibility (WCAG 2.1 AA)
- Verify MudBlazor components have proper ARIA attributes
- Add aria-label to custom components
- Verify keyboard navigation works on all pages
- Verify color contrast ratios meet AA standards
- Verify form error messages are announced by screen readers
- Add skip-to-content link
- Test with screen reader (NVDA/Narrator)

### Error Handling
- Global exception handler middleware for API
- Global error boundary component for Blazor pages
- User-friendly error pages (404, 500)
- Validation error display consistency across all forms

## Wave 6 — Integration Tests

### Test Infrastructure (SupportHub.Tests.Integration)
- WebApplicationFactory<Program> setup
- In-memory or LocalDB SQL Server for tests
- Test authentication helper (mock Azure AD claims)
- Test data seeding helpers
- IServiceCollection overrides for external services (Graph API mock, file storage mock)

### Integration Test Suites
- **Auth Tests**: login redirect, role-based access, company isolation
- **Company Tests**: full CRUD through API, cascading effects
- **Ticket Lifecycle Tests**: create → assign → reply → resolve → close → rate
- **Email Processing Tests**: inbound email creates ticket, reply appends, threading works
- **Routing Tests**: rules evaluate correctly, default fallback, auto-assign
- **SLA Tests**: breach detection across ticket lifecycle
- **Report Tests**: audit report returns correct entries, ticket report filters work, CSV export valid
- **KB Tests**: article CRUD, search, slug generation, publish workflow
- **Concurrent Access Tests**: optimistic concurrency on ticket updates

### Test Data Builders
```csharp
public class TicketBuilder
{
    private Guid _companyId = Guid.NewGuid();
    private string _subject = "Test Ticket";
    // ... fluent builder methods
    public Ticket Build() => new Ticket { ... };
}
```

## Wave 7 — Final Review & Documentation

### Code Quality
- Run `dotnet format` to ensure consistent code style
- Review all TODO/HACK comments and resolve or create tickets
- Verify no unused using statements
- Verify no dead code paths
- Run static analysis (nullable warnings, etc.)

### Configuration Review
- Verify all secrets use environment variables or Key Vault (no hardcoded secrets)
- Verify appsettings.json has appropriate defaults
- Verify appsettings.Development.json exists for local dev
- Document required environment variables

### Deployment Preparation
- Verify EF migrations can be applied to a fresh database
- Verify EF migrations can be applied incrementally (upgrade path)
- Verify Hangfire dashboard is secured
- Verify health check endpoints work
- Document deployment steps

## Acceptance Criteria
- [ ] All CUD operations write audit log entries (verified by integration tests)
- [ ] Company isolation verified — cross-company access denied
- [ ] All API endpoints have proper authorization attributes
- [ ] All input validation in place (no oversized inputs, invalid formats)
- [ ] Performance indexes added and migration runs
- [ ] No N+1 query issues (verified by query logging)
- [ ] Read-only queries use AsNoTracking
- [ ] Caching implemented for frequently-read data
- [ ] EF retry policy configured
- [ ] Serilog structured logging configured with file + console sinks
- [ ] Health checks pass for all dependencies
- [ ] SignalR hub delivers real-time updates
- [ ] Swagger UI accessible at /swagger
- [ ] WCAG 2.1 AA accessibility basics met
- [ ] Integration test suite passes
- [ ] No hardcoded secrets
- [ ] `dotnet build` — zero errors, zero warnings
- [ ] `dotnet test` — all tests pass (unit + integration)

## Dependencies
- All previous phases (1-6)

## Completion
This phase marks the v1 release candidate. After this phase, the system should be ready for production deployment with monitoring, audit compliance, and acceptable performance.
