# Phase 1 Wave 4 — Authentication & ICurrentUserService

## Completed
- `src/SupportHub.Application/Interfaces/ICurrentUserService.cs`: Full interface — UserId, DisplayName, Email, GetUserRolesAsync, HasAccessToCompanyAsync
- `src/SupportHub.Web/Services/CurrentUserService.cs`: Full implementation using IHttpContextAccessor + SupportHubDbContext
- `src/SupportHub.Web/Program.cs`: Azure AD auth, authorization policies, MudBlazor, Serilog, Infrastructure DI, Controllers, Razor Pages
- `src/SupportHub.Web/appsettings.json`: AzureAd section (placeholder TenantId/ClientId), ConnectionStrings.DefaultConnection

## New Types Available
- `ICurrentUserService` (full) in `SupportHub.Application.Interfaces`
- `CurrentUserService` in `SupportHub.Web.Services`

## Authorization Policies (defined in Program.cs)
- `SuperAdmin` — HasClaim("role", "SuperAdmin")
- `Admin` — HasClaim("role", "Admin") OR "SuperAdmin"
- `Agent` — HasClaim("role", "Agent") OR "Admin" OR "SuperAdmin"
- FallbackPolicy: RequireAuthenticatedUser

## Build Status
`dotnet build SupportHub.slnx` — 0 errors, 0 warnings ✅

## Notes for Wave 5 (Service Interfaces & Implementations)
- `ICompanyService`, `IUserService`, `IAuditService` go in `src/SupportHub.Application/Interfaces/`
- All DTOs go in `src/SupportHub.Application/DTOs/`
- Implementations go in `src/SupportHub.Infrastructure/Services/`
- Register services in `src/SupportHub.Infrastructure/DependencyInjection.cs` (AddInfrastructure extension)
- `IAuditService` uses `ICurrentUserService` + `IHttpContextAccessor` (inject into AuditService)
- `UserService.SyncUserFromAzureAdAsync` needs `ICurrentUserService` to read claims
- All service methods return `Task<Result<T>>` with `CancellationToken ct = default`
