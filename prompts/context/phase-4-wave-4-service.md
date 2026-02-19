# Phase 4 Wave 4 — Service Agent Context

## Files Created

### `src/SupportHub.Infrastructure/Services/QueueService.cs`
Implements `IQueueService` with full CRUD operations and company isolation.

### `src/SupportHub.Infrastructure/Services/RoutingRuleService.cs`
Implements `IRoutingRuleService` including rule reordering (10, 20, 30... sort order).

### `src/SupportHub.Infrastructure/Services/RoutingEngine.cs`
Implements `IRoutingEngine` — evaluates ordered routing rules against a `RoutingContext`, falls back to the company default queue if no rule matches.

## Files Modified

### `src/SupportHub.Infrastructure/Services/TicketService.cs`
- Added `IRoutingEngine _routingEngine` to primary constructor.
- After ticket creation and initial `SaveChangesAsync`, calls `_routingEngine.EvaluateAsync` with a `RoutingContext` built from the ticket fields.
- Applies QueueId, AutoAssignAgentId, AutoSetPriority, and AutoAddTags if routing result provides them.

### `src/SupportHub.Infrastructure/Services/EmailProcessingService.cs`
- Added `IRoutingEngine _routingEngine` to primary constructor.
- After new ticket creation from email (and AI classification save), calls `_routingEngine.EvaluateAsync` with email-specific context (SenderDomain extracted from sender email, RequesterEmail set).
- Applies QueueId if routing result provides it.
- Added private `ExtractDomain(string email)` helper.

### `tests/SupportHub.Tests.Unit/Services/TicketServiceTests.cs`
- Added `IRoutingEngine` mock (NSubstitute), defaulting to return a no-op `RoutingResult` (all nulls, IsDefaultFallback=false).
- Added `SupportHub.Application.Common` using for `Result<T>`.
- Updated `TicketService` constructor call to include the routing engine mock.

### `tests/SupportHub.Tests.Unit/Services/EmailProcessingServiceTests.cs`
- Added `IRoutingEngine` mock (NSubstitute), defaulting to return a no-op `RoutingResult`.
- Updated `EmailProcessingService` constructor call to include the routing engine mock.

## Key Implementation Decisions

### QueueService
- `GetQueuesAsync` projects ticket counts in a single DB query using `q.Tickets.Count(t => !t.IsDeleted)` in the LINQ Select.
- Default queue enforcement: when `IsDefault=true` on create/update, all other queues for the same company have `IsDefault` unset before saving.
- Delete guard: checks for active (non-soft-deleted) tickets in the queue; returns failure if any exist.

### RoutingRuleService
- `GetRulesAsync` returns rules ordered by `SortOrder` ascending, active rules only (global query filter handles IsDeleted).
- `CreateRuleAsync` auto-assigns SortOrder as MAX(existing) + 10, defaulting to 10 if no rules exist.
- `ReorderRulesAsync` validates all provided IDs belong to the company (count check), then assigns SortOrder = (position+1)*10.

### RoutingEngine
- Loads active, non-deleted rules with `.Include(r => r.Queue)` for a single DB roundtrip.
- Returns first matching rule result immediately (ordered evaluation, short-circuit).
- On no match, queries for the company's default queue separately.
- Tag matching: `In` operator checks if any context tag is in the comma-separated list; other operators check if any tag satisfies the condition.
- Regex evaluation wrapped in try/catch, returns false on invalid patterns.
- `AutoAddTags` parsed by splitting on comma, trimming, filtering empty strings.

### Double-routing in email flow
`TicketService.CreateTicketAsync` runs routing first (without SenderDomain since it's called from EmailProcessingService with null). `EmailProcessingService` then runs routing again with the actual SenderDomain from the sender email — this second call may override QueueId with more specific email-domain routing. This is intentional per spec.

## Build Status

**Build: SUCCEEDED — 0 errors, 15 warnings (all pre-existing Razor nullable warnings)**
**Tests: 107 passed, 0 failed, 0 skipped**
