# Phase 4 Wave 3 — Complete

## Status
Build: PASSED (0 errors, 15 pre-existing CS8669 warnings from auto-generated Razor code — not related to Phase 4)

## Files Created

### DTOs
- `src/SupportHub.Application/DTOs/QueueDtos.cs`
  - `QueueDto` — response record (all enum fields as `string`)
  - `CreateQueueRequest` — request record (CompanyId, Name, Description?, IsDefault)
  - `UpdateQueueRequest` — request record (Name, Description?, IsDefault, IsActive)

- `src/SupportHub.Application/DTOs/RoutingRuleDtos.cs`
  - `RoutingRuleDto` — response record (MatchType, MatchOperator, AutoSetPriority stored as `string` for serialization)
  - `CreateRoutingRuleRequest` — request record (uses typed enums: `RuleMatchType`, `RuleMatchOperator`, `TicketPriority?`)
  - `UpdateRoutingRuleRequest` — request record (uses typed enums; includes `IsActive`)
  - `ReorderRoutingRulesRequest` — wraps `IReadOnlyList<Guid> RuleIdsInOrder`
  - `RoutingContext` — input to the routing engine (CompanyId, SenderDomain?, Subject, Body, IssueType?, System?, RequesterEmail?, Tags)
  - `RoutingResult` — output from the routing engine (QueueId?, QueueName?, AutoAssignAgentId?, AutoSetPriority?, AutoAddTags, MatchedRuleId?, MatchedRuleName?, IsDefaultFallback)

### Service Interfaces
- `src/SupportHub.Application/Interfaces/IQueueService.cs`
  - `GetQueuesAsync(Guid companyId, int page, int pageSize, CancellationToken)` → `Result<PagedResult<QueueDto>>`
  - `GetQueueByIdAsync(Guid id, CancellationToken)` → `Result<QueueDto>`
  - `CreateQueueAsync(CreateQueueRequest, CancellationToken)` → `Result<QueueDto>`
  - `UpdateQueueAsync(Guid id, UpdateQueueRequest, CancellationToken)` → `Result<QueueDto>`
  - `DeleteQueueAsync(Guid id, CancellationToken)` → `Result<bool>`

- `src/SupportHub.Application/Interfaces/IRoutingRuleService.cs`
  - `GetRulesAsync(Guid companyId, CancellationToken)` → `Result<IReadOnlyList<RoutingRuleDto>>`
  - `GetRuleByIdAsync(Guid id, CancellationToken)` → `Result<RoutingRuleDto>`
  - `CreateRuleAsync(CreateRoutingRuleRequest, CancellationToken)` → `Result<RoutingRuleDto>`
  - `UpdateRuleAsync(Guid id, UpdateRoutingRuleRequest, CancellationToken)` → `Result<RoutingRuleDto>`
  - `DeleteRuleAsync(Guid id, CancellationToken)` → `Result<bool>`
  - `ReorderRulesAsync(Guid companyId, ReorderRoutingRulesRequest, CancellationToken)` → `Result<bool>`

- `src/SupportHub.Application/Interfaces/IRoutingEngine.cs`
  - `EvaluateAsync(RoutingContext, CancellationToken)` → `Result<RoutingResult>`

## Existing Types Used
- `Result<T>` — `SupportHub.Application.Common`
- `PagedResult<T>` — `SupportHub.Application.Common`
- `RuleMatchType` — `SupportHub.Domain.Enums` (values: SenderDomain, SubjectKeyword, BodyKeyword, IssueType, System, Tag, CompanyCode, RequesterEmail)
- `RuleMatchOperator` — `SupportHub.Domain.Enums` (values: Equals, Contains, StartsWith, EndsWith, Regex, In)
- `TicketPriority` — `SupportHub.Domain.Enums` (values: Low, Medium, High, Urgent)

## Notes for Wave 4 (Service Implementation Agents)

### QueueService implementation notes
- `GetQueuesAsync` must enforce `CompanyId` isolation — filter by `companyId` in query
- `IsDefault` enforcement: when creating/updating a queue as `IsDefault = true`, the existing default queue for that company must be unset first (only one default per company)
- `DeleteQueueAsync` must guard against deleting the last active queue or a queue with active tickets — use soft-delete (`IsDeleted`, `DeletedAt`, `DeletedBy`)
- `TicketCount` in `QueueDto` should be computed from related `Ticket` entities (non-deleted, non-closed)
- `CompanyName` in `QueueDto` must be populated via join/include on `Company`

### RoutingRuleService implementation notes
- `GetRulesAsync` returns ALL rules for company ordered by `SortOrder ASC` (no pagination — rules list is bounded)
- `ReorderRulesAsync` updates `SortOrder` of each rule to match the supplied `RuleIdsInOrder` index position (0-based or 1-based — pick 1-based consistently)
- All rules must belong to the same company as validated by `companyId` parameter
- Soft-delete applies; `IsDeleted` filter at query layer
- `AutoAssignAgentName` in `RoutingRuleDto` requires join/include on agent user

### RoutingEngine implementation notes
- Evaluates rules in `SortOrder ASC` order — first matching rule wins
- Match logic per `RuleMatchType` + `RuleMatchOperator` combination
- `RuleMatchOperator.In` means the `MatchValue` is a comma-separated list — check if input value is in that list
- `RuleMatchOperator.Regex` requires `System.Text.RegularExpressions.Regex.IsMatch`
- If no rule matches, fall back to the company's default queue (`IsDefault = true`) and set `IsDefaultFallback = true`
- If no default queue exists, return `Result<RoutingResult>.Failure("No matching rule and no default queue configured")`
- `AutoAddTags` field on `RoutingResult` should be split from the matched rule's `AutoAddTags` string (comma-separated)
- AI classification outcomes must be recorded for audit (see `IAiClassificationService`)
- All rule evaluations must be scoped to `CompanyId` from the `RoutingContext`

### DI Registration (infra-agent owns this)
Register in `src/SupportHub.Infrastructure/DependencyInjection.cs`:
```csharp
services.AddScoped<IQueueService, QueueService>();
services.AddScoped<IRoutingRuleService, RoutingRuleService>();
services.AddScoped<IRoutingEngine, RoutingEngine>();
```

### Domain entities expected (from Wave 1/2 of Phase 4)
- `Queue` entity with: `Id`, `CompanyId`, `Name`, `Description`, `IsDefault`, `IsActive`, `SortOrder`, plus `BaseEntity` fields
- `RoutingRule` entity with: `Id`, `CompanyId`, `QueueId`, `Name`, `Description`, `MatchType` (enum), `MatchOperator` (enum), `MatchValue`, `SortOrder`, `IsActive`, `AutoAssignAgentId`, `AutoSetPriority` (nullable enum), `AutoAddTags`, plus `BaseEntity` fields
