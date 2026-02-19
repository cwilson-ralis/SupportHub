# Phase 1 Wave 2 — Domain Entities

## Completed
- `src/SupportHub.Domain/Entities/Company.cs`: Name, Code, IsActive, Description; Divisions + UserCompanyRoles collections
- `src/SupportHub.Domain/Entities/Division.cs`: CompanyId FK, Name, IsActive; Company navigation
- `src/SupportHub.Domain/Entities/ApplicationUser.cs`: AzureAdObjectId, Email, DisplayName, IsActive; UserCompanyRoles collection
- `src/SupportHub.Domain/Entities/UserCompanyRole.cs`: UserId FK, CompanyId FK, Role (UserRole enum); User + Company navigations
- `src/SupportHub.Domain/Entities/AuditLogEntry.cs`: Immutable — no BaseEntity inheritance; own Guid Id, Timestamp, UserId, UserDisplayName, Action, EntityType, EntityId, OldValues, NewValues, IpAddress, AdditionalData

## New Types Available
- `SupportHub.Domain.Entities.Company`
- `SupportHub.Domain.Entities.Division`
- `SupportHub.Domain.Entities.ApplicationUser`
- `SupportHub.Domain.Entities.UserCompanyRole`
- `SupportHub.Domain.Entities.AuditLogEntry`

## Build Status
`dotnet build SupportHub.slnx` — 0 errors, 0 warnings ✅

## Notes for Next Wave (Wave 3 — EF Core)
- All entities with BaseEntity need soft-delete global query filter: `builder.HasQueryFilter(e => !e.IsDeleted)`
- AuditLogEntry has NO soft-delete filter (immutable)
- UserRole enum stored as string: `.HasConversion<string>().HasMaxLength(50)`
- All FK relationships use `DeleteBehavior.Restrict` (no cascades)
- Division name unique per company: composite index `(CompanyId, Name)`
- UserCompanyRole unique per user+company+role: composite index `(UserId, CompanyId, Role)`
- DbContext file: `src/SupportHub.Infrastructure/Data/SupportHubDbContext.cs`
- Entity configs: `src/SupportHub.Infrastructure/Data/Configurations/`
- Interceptor: `src/SupportHub.Infrastructure/Data/Interceptors/AuditableEntityInterceptor.cs`
- Note: AuditableEntityInterceptor needs `ICurrentUserService` — but ICurrentUserService is defined in Wave 4/5. In Wave 3, define the interface stub or inject a simple `ICurrentUserService` interface with just `UserId` and `DisplayName` properties that the interceptor uses. The full ICurrentUserService is finalized in Wave 4.
