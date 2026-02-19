# Phase 4 Wave 1 — Domain Entities & Enums

## Status
Build: SUCCEEDED — 0 errors, 15 warnings (all pre-existing CS8669 in auto-generated Razor code, unrelated to Wave 1 changes)

## Files Created

### New Enums
- `src/SupportHub.Domain/Enums/RuleMatchType.cs`
  - Values: `SenderDomain`, `SubjectKeyword`, `BodyKeyword`, `IssueType`, `System`, `Tag`, `CompanyCode`, `RequesterEmail`
- `src/SupportHub.Domain/Enums/RuleMatchOperator.cs`
  - Values: `Equals`, `Contains`, `StartsWith`, `EndsWith`, `Regex`, `In`

### New Entities
- `src/SupportHub.Domain/Entities/Queue.cs`
  - Inherits `BaseEntity`
  - Fields: `CompanyId` (Guid), `Name` (string), `Description` (string?), `IsDefault` (bool), `IsActive` (bool)
  - Navigation: `Company`, `RoutingRules` (ICollection<RoutingRule>), `Tickets` (ICollection<Ticket>)
- `src/SupportHub.Domain/Entities/RoutingRule.cs`
  - Inherits `BaseEntity`
  - Fields: `CompanyId` (Guid), `QueueId` (Guid), `Name` (string), `Description` (string?), `MatchType` (RuleMatchType), `MatchOperator` (RuleMatchOperator), `MatchValue` (string), `SortOrder` (int), `IsActive` (bool), `AutoAssignAgentId` (Guid?), `AutoSetPriority` (TicketPriority?), `AutoAddTags` (string?)
  - Navigation: `Company`, `Queue`, `AutoAssignAgent` (ApplicationUser?)

## Files Modified

### `src/SupportHub.Domain/Entities/Ticket.cs`
- Added nullable navigation property: `public Queue? Queue { get; set; } = null;`
  - Positioned after `Company` navigation, before `AssignedAgent`
  - Corresponds to the existing nullable FK `QueueId` already on the entity

### `src/SupportHub.Infrastructure/Data/SupportHubDbContext.cs`
- Added two new DbSets after `EmailProcessingLogs`:
  ```csharp
  public DbSet<Queue> Queues => Set<Queue>();
  public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();
  ```

## New Types Available for Wave 2+

| Type | Namespace | Notes |
|------|-----------|-------|
| `RuleMatchType` | `SupportHub.Domain.Enums` | Enum — 8 values |
| `RuleMatchOperator` | `SupportHub.Domain.Enums` | Enum — 6 values |
| `Queue` | `SupportHub.Domain.Entities` | Entity with CompanyId FK |
| `RoutingRule` | `SupportHub.Domain.Entities` | Entity with CompanyId + QueueId FKs |

## Notes for Wave 2 (EF Configurations Agent)

Wave 2 must create `IEntityTypeConfiguration<T>` classes in `src/SupportHub.Infrastructure/Data/Configurations/`:

### `QueueConfiguration.cs`
- Table name: `Queues`
- `CompanyId` — required FK to `Companies`; restrict delete to avoid cascades wiping all queues
- `Name` — required, max length ~200
- Global query filter: `.HasQueryFilter(q => !q.IsDeleted)`
- One `Company` has many `Queues`
- One `Queue` has many `RoutingRules` (via `RoutingRule.QueueId`)
- One `Queue` has many `Tickets` (via `Ticket.QueueId`) — this FK already exists on `Ticket`

### `RoutingRuleConfiguration.cs`
- Table name: `RoutingRules`
- `CompanyId` — required FK to `Companies`; restrict delete
- `QueueId` — required FK to `Queues`; restrict delete (deleting a queue should not cascade-delete rules silently)
- `Name` — required, max length ~200
- `MatchValue` — required, max length ~500
- `AutoAssignAgentId` — optional FK to `ApplicationUsers`; set null on delete
- `AutoAddTags` — optional, max length ~500
- Global query filter: `.HasQueryFilter(r => !r.IsDeleted)`
- `MatchType` and `MatchOperator` are stored as int (EF default for enums)

### Ticket FK already exists
- `Ticket.QueueId` FK to `Queues` already exists in the domain. The `TicketConfiguration` (if it exists from Phase 2) may need updating to define the relationship explicitly now that `Queue` entity is registered. Check `src/SupportHub.Infrastructure/Data/Configurations/TicketConfiguration.cs`.

### DTOs / Application Layer
Wave 2 or 3 agents should create DTOs (records) in `src/SupportHub.Application/DTOs/`:
- `QueueDto`, `CreateQueueRequest`, `UpdateQueueRequest`
- `RoutingRuleDto`, `CreateRoutingRuleRequest`, `UpdateRoutingRuleRequest`

### Service Interfaces
Wave 2 or 3 agents should create service interfaces in `src/SupportHub.Application/Interfaces/`:
- `IQueueService` — CRUD + list by company + set-default
- `IRoutingRuleService` — CRUD + list ordered by SortOrder + reorder + evaluate pipeline

### Migration
A new EF migration must be added after EF configurations are in place:
```bash
dotnet ef migrations add Phase4_RoutingQueues --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
```
