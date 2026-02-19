# Phase 4 Wave 2 — EF Configurations & Migration

## Status
COMPLETE — Build succeeded, 107 tests pass (0 failures).

## Files Created

### `src/SupportHub.Infrastructure/Data/Configurations/QueueConfiguration.cs`
- Table: `Queues`
- HasQueryFilter: `!q.IsDeleted`
- Indexes:
  - Unique composite: `(CompanyId, Name)`
  - Filtered unique: `(CompanyId, IsDefault)` with filter `IsDefault = 1` — enforces one default queue per company
  - `IsActive`
- Relationships:
  - `Company` → Restrict (FK: CompanyId)
  - `RoutingRules` (HasMany → WithOne) → Restrict (FK: QueueId) — owns this side of the relationship
  - `Tickets` (HasMany → WithOne) → Restrict (FK: QueueId) — owns this side of the relationship

### `src/SupportHub.Infrastructure/Data/Configurations/RoutingRuleConfiguration.cs`
- Table: `RoutingRules`
- HasQueryFilter: `!r.IsDeleted`
- Enums stored as strings: `MatchType` (max 50), `MatchOperator` (max 50), `AutoSetPriority` (nullable, max 20)
- Indexes:
  - Composite: `(CompanyId, SortOrder)`
  - `QueueId`
  - `IsActive`
- Relationships:
  - `Company` → Restrict (FK: CompanyId)
  - `Queue` relationship NOT defined here — owned by `QueueConfiguration` to avoid EF conflict
  - `AutoAssignAgent` → SetNull (FK: AutoAssignAgentId, nullable)

## Files Modified

### `src/SupportHub.Infrastructure/Data/Configurations/TicketConfiguration.cs`
- Added: `builder.HasIndex(t => t.QueueId);`
- Queue FK relationship NOT defined here — owned by `QueueConfiguration` (HasMany Tickets → WithOne Queue)
- No other changes

## Migration Generated
- Name: `AddRoutingAndQueues`
- File: `src/SupportHub.Infrastructure/Data/Migrations/20260219064829_AddRoutingAndQueues.cs`
- Creates tables: `Queues`, `RoutingRules`
- Adds FK column `QueueId` on `Tickets` (already existed as nullable Guid on entity from Wave 1)

## Notes for Wave 3 (DTOs + Interfaces Agent)

### Key patterns to follow
- Queue FK on Ticket is nullable (`Guid?`) — tickets can exist without a queue assignment
- The filtered unique index on Queue `(CompanyId, IsDefault)` with `IsDefault = 1` means EF will enforce at most one `IsDefault = true` queue per company at the DB level
- `RoutingRule.AutoAddTags` is a single string (comma-delimited or JSON — parsing is an application concern)
- `RoutingRule.AutoSetPriority` is nullable `TicketPriority?` stored as string

### DTOs needed (Wave 3)
- `QueueDto`, `CreateQueueRequest`, `UpdateQueueRequest`
- `RoutingRuleDto`, `CreateRoutingRuleRequest`, `UpdateRoutingRuleRequest`
- `ReorderRoutingRulesRequest` (for drag-and-drop SortOrder updates)

### Service interfaces needed (Wave 3)
- `IQueueService`: CreateAsync, UpdateAsync, DeleteAsync, GetByIdAsync, GetByCompanyAsync, SetDefaultAsync
- `IRoutingRuleService`: CreateAsync, UpdateAsync, DeleteAsync, GetByCompanyAsync, ReorderAsync, EvaluateAsync (runs rules pipeline against a ticket)

### Important: Company isolation
All queue and routing rule queries MUST filter by CompanyId. The `HasQueryFilter(!IsDeleted)` is already applied.

## Build Status
- Build: SUCCEEDED (0 errors, 15 pre-existing CS8669 warnings in Razor auto-generated code)
- Tests: 107 passed, 0 failed
