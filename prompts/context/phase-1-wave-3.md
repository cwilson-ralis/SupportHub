# Phase 1 Wave 3 — EF Core Configuration, DbContext & Migration

## Completed
- `src/SupportHub.Application/Interfaces/ICurrentUserService.cs`: Stub — UserId, DisplayName, Email string? properties
- `src/SupportHub.Infrastructure/Data/SupportHubDbContext.cs`: 5 DbSets, ApplyConfigurationsFromAssembly
- `src/SupportHub.Infrastructure/Data/Interceptors/AuditableEntityInterceptor.cs`: Sync + async SavingChanges, stamps Created/Updated/Deleted fields
- `src/SupportHub.Infrastructure/Data/SupportHubDbContextFactory.cs`: IDesignTimeDbContextFactory for EF CLI tooling
- `src/SupportHub.Infrastructure/Data/Configurations/CompanyConfiguration.cs`
- `src/SupportHub.Infrastructure/Data/Configurations/DivisionConfiguration.cs`
- `src/SupportHub.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs`
- `src/SupportHub.Infrastructure/Data/Configurations/UserCompanyRoleConfiguration.cs`
- `src/SupportHub.Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs`
- `src/SupportHub.Infrastructure/DependencyInjection.cs`: AddInfrastructure() extension, registers AuditableEntityInterceptor + DbContext
- `src/SupportHub.Infrastructure/Data/Migrations/20260219042733_InitialCreate.cs`: All 5 tables

## New Interfaces Available
- `ICurrentUserService` in `SupportHub.Application.Interfaces` — stub (string? UserId, DisplayName, Email)
  - Wave 4 will expand this with GetUserRolesAsync and HasAccessToCompanyAsync

## Build Status
`dotnet build SupportHub.slnx` — 0 errors, 0 warnings ✅

## Notes for Wave 4 (Authentication & ICurrentUserService)
- `ICurrentUserService` is already defined in Application/Interfaces — Wave 4 MUST EXPAND it (add async methods) not replace it
- The full interface needs: `Task<IReadOnlyList<UserCompanyRole>> GetUserRolesAsync(CancellationToken ct = default)` and `Task<bool> HasAccessToCompanyAsync(Guid companyId, CancellationToken ct = default)`
- Implementation goes in `src/SupportHub.Web/Services/CurrentUserService.cs`
- Auth config goes in `src/SupportHub.Web/Program.cs`
- `IHttpContextAccessor` needed — register in Program.cs: `builder.Services.AddHttpContextAccessor()`
- `builder.Services.AddInfrastructure(builder.Configuration)` should be called in Program.cs
- Design-time connection string in `SupportHubDbContextFactory.cs` uses: `(localdb)\mssqllocaldb;Database=SupportHub_Dev`
