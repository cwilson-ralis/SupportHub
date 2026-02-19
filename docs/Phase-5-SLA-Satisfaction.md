# Phase 5 — SLA & Customer Satisfaction

## Overview
SLA policy configuration per company/priority, background monitoring job that detects breaches, SLA indicators on ticket views, and customer satisfaction (CSAT) survey on ticket close.

## Prerequisites
- Phase 2 complete (Ticket entity with FirstResponseAt, ResolvedAt, ClosedAt)
- Phase 4 complete (Queues for SLA scoping)
- Hangfire configured (Phase 3)

## Wave 1 — Domain Entities

### SlaPolicy Entity
- Id (Guid, PK)
- CompanyId (Guid, FK→Company, required)
- Name (string, required, max 200)
- Description (string?, max 1000)
- Priority (TicketPriority — which priority this policy applies to)
- FirstResponseTargetMinutes (int — target time in minutes for first response)
- ResolutionTargetMinutes (int — target time in minutes for resolution)
- IsActive (bool, default true)
- Plus BaseEntity fields
- Navigation: Company
- Unique constraint: CompanyId + Priority (one policy per company per priority)

### SlaBreachRecord Entity
- Id (Guid, PK)
- TicketId (Guid, FK→Ticket, required)
- SlaPolicyId (Guid, FK→SlaPolicy, required)
- BreachType (SlaBreachType enum: FirstResponse, Resolution)
- TargetMinutes (int — snapshot of the target at time of breach)
- ActualMinutes (int — actual elapsed minutes)
- BreachedAt (DateTimeOffset)
- AcknowledgedAt (DateTimeOffset?)
- AcknowledgedBy (string?)
- Plus BaseEntity fields
- Navigations: Ticket, SlaPolicy

### SlaBreachType Enum
```csharp
public enum SlaBreachType
{
    FirstResponse,
    Resolution
}
```

### CustomerSatisfactionRating Entity
- Id (Guid, PK)
- TicketId (Guid, FK→Ticket, required, unique — one rating per ticket)
- Rating (int, required, range 1-5)
- Comment (string?, max 2000)
- SubmittedAt (DateTimeOffset)
- SubmittedByEmail (string, required, max 256)
- Plus BaseEntity fields
- Navigation: Ticket

### Ticket Entity Updates
Add navigation properties to Ticket (no schema changes needed if designed correctly):
- SlaBreachRecords (collection navigation)
- CustomerSatisfactionRating (reference navigation)
- Computed/transient properties for SLA status (not persisted, calculated at query time)

## Wave 2 — EF Configurations & Migration

### SlaPolicyConfiguration
- Table: SlaPolicies
- PK: Id
- FK: CompanyId → Companies (Restrict)
- Unique index: CompanyId + Priority
- Index: IsActive
- MaxLength: Name 200, Description 1000
- Priority stored as string

### SlaBreachRecordConfiguration
- Table: SlaBreachRecords
- PK: Id
- FK: TicketId → Tickets (Cascade), SlaPolicyId → SlaPolicies (Restrict)
- Index: TicketId, SlaPolicyId, BreachedAt, BreachType
- BreachType stored as string

### CustomerSatisfactionRatingConfiguration
- Table: CustomerSatisfactionRatings
- PK: Id
- FK: TicketId → Tickets (Cascade)
- Unique index: TicketId (one per ticket)
- Index: Rating, SubmittedAt
- Check constraint: Rating between 1 and 5

### Migration
`dotnet ef migrations add AddSlaAndSatisfaction`

## Wave 3 — Service Interfaces & DTOs

### DTOs
```csharp
public record SlaPolicyDto(Guid Id, Guid CompanyId, string CompanyName, string Name, string? Description, TicketPriority Priority, int FirstResponseTargetMinutes, int ResolutionTargetMinutes, bool IsActive);
public record CreateSlaPolicyRequest(Guid CompanyId, string Name, string? Description, TicketPriority Priority, int FirstResponseTargetMinutes, int ResolutionTargetMinutes);
public record UpdateSlaPolicyRequest(string Name, string? Description, int FirstResponseTargetMinutes, int ResolutionTargetMinutes, bool IsActive);

public record SlaBreachRecordDto(Guid Id, Guid TicketId, string TicketNumber, Guid SlaPolicyId, string SlaPolicyName, SlaBreachType BreachType, int TargetMinutes, int ActualMinutes, DateTimeOffset BreachedAt, DateTimeOffset? AcknowledgedAt, string? AcknowledgedBy);

public record SlaStatusDto(bool IsFirstResponseBreached, bool IsResolutionBreached, int? FirstResponseTargetMinutes, int? FirstResponseElapsedMinutes, int? ResolutionTargetMinutes, int? ResolutionElapsedMinutes, double? FirstResponsePercentage, double? ResolutionPercentage);

public record CustomerSatisfactionRatingDto(Guid Id, Guid TicketId, int Rating, string? Comment, DateTimeOffset SubmittedAt, string SubmittedByEmail);
public record SubmitSatisfactionRatingRequest(Guid TicketId, int Rating, string? Comment);

public record SatisfactionSummaryDto(double AverageRating, int TotalRatings, IReadOnlyDictionary<int, int> RatingDistribution);
```

### ISlaPolicyService
```csharp
public interface ISlaPolicyService
{
    Task<Result<PagedResult<SlaPolicyDto>>> GetPoliciesAsync(Guid? companyId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<SlaPolicyDto>> GetPolicyByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<SlaPolicyDto>> CreatePolicyAsync(CreateSlaPolicyRequest request, CancellationToken ct = default);
    Task<Result<SlaPolicyDto>> UpdatePolicyAsync(Guid id, UpdateSlaPolicyRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeletePolicyAsync(Guid id, CancellationToken ct = default);
    Task<Result<SlaStatusDto>> GetTicketSlaStatusAsync(Guid ticketId, CancellationToken ct = default);
}
```

### ISlaMonitoringService
```csharp
public interface ISlaMonitoringService
{
    Task<Result<int>> CheckForBreachesAsync(CancellationToken ct = default);
    Task<Result<bool>> AcknowledgeBreachAsync(Guid breachId, CancellationToken ct = default);
    Task<Result<PagedResult<SlaBreachRecordDto>>> GetBreachesAsync(Guid? companyId, SlaBreachType? breachType, bool? acknowledged, int page, int pageSize, CancellationToken ct = default);
}
```

### ICustomerSatisfactionService
```csharp
public interface ICustomerSatisfactionService
{
    Task<Result<CustomerSatisfactionRatingDto>> SubmitRatingAsync(SubmitSatisfactionRatingRequest request, CancellationToken ct = default);
    Task<Result<CustomerSatisfactionRatingDto?>> GetRatingForTicketAsync(Guid ticketId, CancellationToken ct = default);
    Task<Result<SatisfactionSummaryDto>> GetSatisfactionSummaryAsync(Guid? companyId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}
```

## Wave 4 — Service Implementations

### SlaPolicyService
- CRUD with company isolation
- Enforce unique CompanyId + Priority constraint
- Validate FirstResponseTargetMinutes and ResolutionTargetMinutes > 0
- GetTicketSlaStatusAsync: load ticket, find applicable SLA policy (by company + priority), calculate elapsed minutes, return SlaStatusDto with percentages

### SlaMonitoringService
Core breach detection logic (CheckForBreachesAsync):
1. Load all active SLA policies
2. For each policy, find tickets where:
   - CompanyId matches and Priority matches
   - Status is not Closed or Resolved
   - No existing breach record for this type
   - Elapsed time exceeds target
3. For first response breaches: tickets where FirstResponseAt is null AND (now - CreatedAt) > FirstResponseTargetMinutes
4. For resolution breaches: tickets where ResolvedAt is null AND (now - CreatedAt) > ResolutionTargetMinutes
5. Create SlaBreachRecord for each new breach
6. Return count of new breaches
7. Log breaches via ILogger

AcknowledgeBreachAsync: set AcknowledgedAt and AcknowledgedBy from ICurrentUserService.

### CustomerSatisfactionService
- SubmitRatingAsync: validate ticket exists, is Closed/Resolved, no existing rating, rating 1-5
- GetRatingForTicketAsync: simple lookup
- GetSatisfactionSummaryAsync: aggregate query with optional company and date filters

### SLA Monitoring Hangfire Job
```csharp
public class SlaMonitoringJob
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Call ISlaMonitoringService.CheckForBreachesAsync
        // Log results
    }
}
```
Registered as recurring job, runs every 5 minutes.

## Wave 5 — Blazor UI

### SLA Policy Admin Page (`/admin/sla-policies`)
- MudDataGrid grouped by company
- Columns: Company, Priority, Name, First Response Target, Resolution Target, Active
- Add/Edit dialog with validation
- Target times displayed in human-readable format (e.g., "4 hours", "1 day")

### SLA Breaches Page (`/admin/sla-breaches`)
- MudDataGrid with filters: company, breach type, acknowledged/unacknowledged, date range
- Columns: Ticket Number (link), Company, Breach Type, Target, Actual, Breached At, Status
- Acknowledge action button
- Color coding: red for unacknowledged, yellow for acknowledged

### Ticket List SLA Indicator
- Add SLA status column to ticket list MudDataGrid
- MudProgressLinear or colored icon:
  - Green: within SLA (< 75% of target)
  - Yellow: approaching breach (75-99% of target)
  - Red: breached (>= 100% of target)

### Ticket Detail SLA Panel
- SLA status card in ticket detail sidebar
- Show first response and resolution progress bars
- Show breach records if any
- Timer showing time remaining/overdue

### CSAT Survey Component
- Shown to requester when ticket is Closed/Resolved
- 5-star rating with optional comment
- "Thank you" confirmation after submission
- Read-only display of existing rating for agents

### CSAT Summary Widget
- Small summary card for dashboard (used in Phase 6 dashboard)
- Average rating, total ratings, distribution bar chart

## Wave 6 — API Controllers

### SlaPoliciesController
- GET /api/sla-policies?companyId={id} — list policies
- GET /api/sla-policies/{id} — single policy
- POST /api/sla-policies — create
- PUT /api/sla-policies/{id} — update
- DELETE /api/sla-policies/{id} — soft-delete
- GET /api/tickets/{ticketId}/sla-status — get SLA status for ticket

### SlaBreachesController
- GET /api/sla-breaches — list breaches with filters
- POST /api/sla-breaches/{id}/acknowledge — acknowledge breach

### CustomerSatisfactionController
- POST /api/satisfaction — submit rating
- GET /api/tickets/{ticketId}/satisfaction — get rating for ticket
- GET /api/satisfaction/summary — get aggregate summary

## Wave 7 — Tests

### Unit Tests
- SlaPolicyServiceTests: CRUD, unique constraint, company isolation, SLA status calculation (within SLA, approaching, breached)
- SlaMonitoringServiceTests: detect first response breach, detect resolution breach, skip already-breached, skip resolved tickets, acknowledge breach
- CustomerSatisfactionServiceTests: submit rating, duplicate prevention, invalid rating range, summary aggregation
- SlaMonitoringJobTests: runs check, handles errors

### SLA Calculation Test Scenarios
- Ticket created 30 min ago, policy is 60 min → 50% (green)
- Ticket created 50 min ago, policy is 60 min → 83% (yellow)
- Ticket created 90 min ago, policy is 60 min → 150% (red/breached)
- Ticket already has FirstResponseAt → no first-response breach check
- Ticket is Resolved → no resolution breach check

## Acceptance Criteria
- [ ] SlaPolicy, SlaBreachRecord, CustomerSatisfactionRating entities with proper EF config
- [ ] Migration runs successfully
- [ ] SLA policies configurable per company per priority
- [ ] One SLA policy per company per priority enforced
- [ ] SLA monitoring job detects breaches correctly
- [ ] SLA indicators show on ticket list and detail
- [ ] Color coding reflects SLA status (green/yellow/red)
- [ ] CSAT survey appears on closed/resolved tickets
- [ ] Only one rating per ticket enforced
- [ ] CSAT summary aggregation works
- [ ] SLA breaches can be acknowledged
- [ ] All unit tests pass
- [ ] `dotnet build` — zero errors

## Dependencies
- Phase 2: Ticket (FirstResponseAt, ResolvedAt, ClosedAt), TicketStatus
- Phase 3: Hangfire infrastructure
- Phase 4: Queue (for SLA scoping context)

## Next Phase
Phase 6 (KB & Reporting) builds audit/SOX reports, dashboards with SLA and CSAT data, and knowledge base articles.
