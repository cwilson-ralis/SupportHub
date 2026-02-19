# Phase 4 — Routing & Queue Management

## Overview
Admin-configurable routing rules engine with ordered evaluation, queue management, and integration into ticket creation and email processing flows. Rules can match on sender domain, keywords, form fields, and tags.

## Prerequisites
- Phase 2 complete (Ticket entity with nullable QueueId)
- Phase 3 complete (email processing pipeline)

## Wave 1 — Domain Entities & Enums

### Enums
```csharp
public enum RuleMatchType
{
    SenderDomain,
    SubjectKeyword,
    BodyKeyword,
    IssueType,
    System,
    Tag,
    CompanyCode,
    RequesterEmail
}

public enum RuleMatchOperator
{
    Equals,
    Contains,
    StartsWith,
    EndsWith,
    Regex,
    In  // comma-separated list
}
```

### Queue Entity
- Id (Guid, PK)
- CompanyId (Guid, FK→Company, required)
- Name (string, required, max 200)
- Description (string?, max 1000)
- IsDefault (bool, default false — one default per company)
- IsActive (bool, default true)
- Plus BaseEntity fields
- Navigations: Company, RoutingRules, Tickets

### RoutingRule Entity
- Id (Guid, PK)
- CompanyId (Guid, FK→Company, required)
- QueueId (Guid, FK→Queue, required — destination queue)
- Name (string, required, max 200)
- Description (string?, max 1000)
- MatchType (RuleMatchType)
- MatchOperator (RuleMatchOperator)
- MatchValue (string, required, max 1000)
- SortOrder (int — evaluation order, lower = first)
- IsActive (bool, default true)
- AutoAssignAgentId (Guid?, FK→ApplicationUser, nullable — auto-assign to this agent)
- AutoSetPriority (TicketPriority? — auto-set priority if rule matches)
- AutoAddTags (string?, max 1000 — comma-separated tags to auto-add)
- Plus BaseEntity fields
- Navigations: Company, Queue, AutoAssignAgent

## Wave 2 — EF Configurations & Migration

### QueueConfiguration
- Table: Queues
- PK: Id
- FK: CompanyId → Companies (Restrict)
- Indexes: CompanyId+Name (unique), CompanyId+IsDefault (filtered where IsDefault=true, unique — only one default per company)
- MaxLength: Name 200, Description 1000

### RoutingRuleConfiguration
- Table: RoutingRules
- PK: Id
- FK: CompanyId → Companies (Restrict), QueueId → Queues (Restrict), AutoAssignAgentId → ApplicationUsers (SetNull)
- Indexes: CompanyId+SortOrder, QueueId, IsActive
- MaxLength: Name 200, Description 1000, MatchValue 1000, AutoAddTags 1000
- MatchType and MatchOperator stored as strings (EnumToString conversion)

### Migration
`dotnet ef migrations add AddRoutingAndQueues`

Note: Ticket.QueueId FK was already nullable from Phase 2. This migration adds the Queue and RoutingRule tables and creates the FK constraint from Ticket.QueueId → Queue.Id.

## Wave 3 — Service Interfaces & DTOs

### DTOs
```csharp
public record QueueDto(Guid Id, Guid CompanyId, string Name, string? Description, bool IsDefault, bool IsActive, int TicketCount);
public record CreateQueueRequest(Guid CompanyId, string Name, string? Description, bool IsDefault);
public record UpdateQueueRequest(string Name, string? Description, bool IsDefault, bool IsActive);

public record RoutingRuleDto(Guid Id, Guid CompanyId, Guid QueueId, string QueueName, string Name, string? Description, RuleMatchType MatchType, RuleMatchOperator MatchOperator, string MatchValue, int SortOrder, bool IsActive, Guid? AutoAssignAgentId, string? AutoAssignAgentName, TicketPriority? AutoSetPriority, string? AutoAddTags);
public record CreateRoutingRuleRequest(Guid CompanyId, Guid QueueId, string Name, string? Description, RuleMatchType MatchType, RuleMatchOperator MatchOperator, string MatchValue, Guid? AutoAssignAgentId, TicketPriority? AutoSetPriority, string? AutoAddTags);
public record UpdateRoutingRuleRequest(Guid QueueId, string Name, string? Description, RuleMatchType MatchType, RuleMatchOperator MatchOperator, string MatchValue, bool IsActive, Guid? AutoAssignAgentId, TicketPriority? AutoSetPriority, string? AutoAddTags);
public record ReorderRoutingRulesRequest(IReadOnlyList<Guid> RuleIdsInOrder);

public record RoutingContext(Guid CompanyId, string? SenderDomain, string Subject, string Body, string? IssueType, string? System, string? RequesterEmail, IReadOnlyList<string> Tags);
public record RoutingResult(Guid? QueueId, string? QueueName, Guid? AutoAssignAgentId, TicketPriority? AutoSetPriority, IReadOnlyList<string> AutoAddTags, Guid? MatchedRuleId, string? MatchedRuleName, bool IsDefaultFallback);
```

### IQueueService
```csharp
public interface IQueueService
{
    Task<Result<PagedResult<QueueDto>>> GetQueuesAsync(Guid companyId, int page, int pageSize, CancellationToken ct = default);
    Task<Result<QueueDto>> GetQueueByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<QueueDto>> CreateQueueAsync(CreateQueueRequest request, CancellationToken ct = default);
    Task<Result<QueueDto>> UpdateQueueAsync(Guid id, UpdateQueueRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteQueueAsync(Guid id, CancellationToken ct = default);
}
```

### IRoutingRuleService
```csharp
public interface IRoutingRuleService
{
    Task<Result<IReadOnlyList<RoutingRuleDto>>> GetRulesAsync(Guid companyId, CancellationToken ct = default);
    Task<Result<RoutingRuleDto>> GetRuleByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<RoutingRuleDto>> CreateRuleAsync(CreateRoutingRuleRequest request, CancellationToken ct = default);
    Task<Result<RoutingRuleDto>> UpdateRuleAsync(Guid id, UpdateRoutingRuleRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteRuleAsync(Guid id, CancellationToken ct = default);
    Task<Result<bool>> ReorderRulesAsync(Guid companyId, ReorderRoutingRulesRequest request, CancellationToken ct = default);
}
```

### IRoutingEngine
```csharp
public interface IRoutingEngine
{
    Task<Result<RoutingResult>> EvaluateAsync(RoutingContext context, CancellationToken ct = default);
}
```

## Wave 4 — Service Implementations

### QueueService
- CRUD with company isolation
- When setting IsDefault=true, unset previous default for that company
- Prevent deletion if queue has active tickets (or soft-delete and reassign)
- Validate company exists and user has access
- Audit logging on all operations

### RoutingRuleService
- CRUD with company isolation
- Auto-assign SortOrder on create (max + 10 for the company)
- Reorder: accept ordered list of rule IDs, reassign SortOrder sequentially (10, 20, 30...)
- Validate QueueId belongs to same company
- Audit logging

### RoutingEngine
Core evaluation logic:
1. Load all active rules for the given CompanyId, ordered by SortOrder ascending
2. For each rule, evaluate match:
   - SenderDomain + Equals: extract domain from email, compare
   - SubjectKeyword/BodyKeyword + Contains: case-insensitive substring
   - SubjectKeyword/BodyKeyword + Regex: regex match
   - IssueType/System + Equals: exact match
   - Tag + In: check if any ticket tag matches comma-separated list
   - etc.
3. First matching rule wins → return RoutingResult with queue, auto-assign, priority, tags
4. No match → find default queue for company → return with IsDefaultFallback=true
5. No default queue → return null QueueId (ticket stays unrouted)

### Integration Points
- Modify TicketService.CreateTicketAsync: after creating ticket, call IRoutingEngine.EvaluateAsync and apply results
- Modify EmailProcessingService: after creating ticket from email, call routing engine
- Both should apply auto-assign, auto-priority, auto-tags from routing result

## Wave 5 — Blazor UI

### Queue Management Page (`/admin/queues`)
- MudDataGrid grouped by company
- Columns: Name, Description, Default indicator, Active status, Ticket count
- Add/Edit dialog with MudForm
- Mark as default action
- Disable delete if queue has tickets

### Routing Rules Page (`/admin/routing-rules`)
- Company selector at top
- MudTable/MudDataGrid showing rules in SortOrder for selected company
- Drag-and-drop reordering (MudDropZone or custom JS interop)
- Columns: Order, Name, Match Type, Match Operator, Match Value, Destination Queue, Active toggle, Actions
- Add/Edit dialog with:
  - Name, description
  - Match type dropdown (changes available operators)
  - Match operator dropdown
  - Match value text field (with hint based on type)
  - Destination queue dropdown (filtered by company)
  - Optional: auto-assign agent, auto-set priority, auto-add tags
- Rule test panel: enter sample ticket data, see which rule would match

### Ticket List Enhancement
- Add Queue column to ticket list
- Add Queue filter to ticket filter

### Ticket Detail Enhancement
- Show assigned queue in ticket properties sidebar
- Allow manual queue reassignment

## Wave 6 — API Controllers

### QueuesController
- GET /api/queues?companyId={id} — list queues for company
- GET /api/queues/{id} — single queue
- POST /api/queues — create
- PUT /api/queues/{id} — update
- DELETE /api/queues/{id} — soft-delete

### RoutingRulesController
- GET /api/routing-rules?companyId={id} — list rules for company (ordered)
- GET /api/routing-rules/{id} — single rule
- POST /api/routing-rules — create
- PUT /api/routing-rules/{id} — update
- DELETE /api/routing-rules/{id} — soft-delete
- POST /api/routing-rules/reorder — reorder rules
- POST /api/routing-rules/test — test routing with sample data

## Wave 7 — Tests

### Unit Tests
- QueueServiceTests: CRUD, default queue toggle, prevent delete with tickets, company isolation
- RoutingRuleServiceTests: CRUD, auto sort order, reorder, company isolation
- RoutingEngineTests:
  - Match sender domain (equals, contains)
  - Match subject keyword (contains, regex)
  - Match body keyword
  - Match issue type (equals)
  - First match wins (order matters)
  - Default queue fallback when no rules match
  - No match and no default → unrouted
  - Inactive rules skipped
  - Auto-assign, auto-priority, auto-tags applied correctly
  - In operator with comma-separated values
- Integration: TicketService calls routing engine on create

## Acceptance Criteria
- [ ] Queue and RoutingRule entities with proper EF configurations
- [ ] Migration runs successfully
- [ ] Queue CRUD works with company isolation
- [ ] Only one default queue per company enforced
- [ ] Routing rules evaluate in sort order
- [ ] First matching rule wins
- [ ] Unmatched tickets fall to default queue
- [ ] Drag-drop rule reordering works
- [ ] Auto-assign, auto-priority, auto-tags applied on match
- [ ] Routing integrates into ticket creation (web form + email)
- [ ] Rule test panel returns correct match
- [ ] Ticket list/detail shows queue
- [ ] All unit tests pass
- [ ] `dotnet build` — zero errors

## Dependencies
- Phase 2: Ticket entity (QueueId nullable FK), ITicketService
- Phase 3: IEmailProcessingService (for routing integration)

## Next Phase
Phase 5 (SLA & Satisfaction) adds SLA policies monitored per-queue and CSAT surveys.
