# Phase 1 Wave 5 — Service Interfaces, DTOs & Implementations

## Completed
- `src/SupportHub.Application/DTOs/CompanyDtos.cs`: CompanyDto, CreateCompanyRequest, UpdateCompanyRequest
- `src/SupportHub.Application/DTOs/DivisionDtos.cs`: DivisionDto, CreateDivisionRequest, UpdateDivisionRequest
- `src/SupportHub.Application/DTOs/UserDtos.cs`: UserDto, UserCompanyRoleDto
- `src/SupportHub.Application/Interfaces/ICompanyService.cs`
- `src/SupportHub.Application/Interfaces/IUserService.cs`
- `src/SupportHub.Application/Interfaces/IAuditService.cs`
- `src/SupportHub.Infrastructure/Services/AuditService.cs`: Creates AuditLogEntry, serializes old/new values as JSON; no IpAddress (Infrastructure is not Web SDK)
- `src/SupportHub.Infrastructure/Services/CompanyService.cs`: Full CRUD with soft-delete, code uniqueness check, pagination, audit logging
- `src/SupportHub.Infrastructure/Services/UserService.cs`: Pagination, Azure AD upsert sync, role assign/remove with guards, audit logging
- `src/SupportHub.Infrastructure/DependencyInjection.cs`: Registers IAuditService, ICompanyService, IUserService

## New Types Available
- `SupportHub.Application.DTOs.CompanyDto`, `CreateCompanyRequest`, `UpdateCompanyRequest`
- `SupportHub.Application.DTOs.DivisionDto`, `CreateDivisionRequest`, `UpdateDivisionRequest`
- `SupportHub.Application.DTOs.UserDto`, `UserCompanyRoleDto`
- `SupportHub.Application.Interfaces.ICompanyService`, `IUserService`, `IAuditService`

## New Endpoints/Services Available (via DI)
- `ICompanyService` → `CompanyService`
- `IUserService` → `UserService`
- `IAuditService` → `AuditService`

## Build Status
`dotnet build SupportHub.slnx` — 0 errors, 0 warnings ✅

## Notes for Wave 6 (UI + API)
- UI agent owns: `src/SupportHub.Web/Components/Pages/` and `src/SupportHub.Web/Components/Layout/`
- API agent owns: `src/SupportHub.Web/Controllers/`
- Inject services via constructor: `ICompanyService`, `IUserService`, `ICurrentUserService`
- All pages use `@attribute [Authorize(Policy = "SuperAdmin")]` except Dashboard (`@attribute [Authorize]`)
- MudBlazor is already registered (Wave 4)
- Controllers need `[ApiController]`, `[Route("api/[controller]")]`, `[Authorize(Policy = "SuperAdmin")]`
- Result pattern: `result.IsSuccess` → 200/201, `!result.IsSuccess` → 400, null entity → 404
- CompanyService.CreateCompanyAsync uses Code.ToUpper() — keep this in mind for display
- DivisionService is NOT yet implemented — Wave 6 UI should note this; Divisions are accessed via Company navigations for now
- For the company detail page showing divisions, directly query DbContext or add a simple IDivisionService — but this is out of scope for Wave 6 UI; use placeholder if needed
