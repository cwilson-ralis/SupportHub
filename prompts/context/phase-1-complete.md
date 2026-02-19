# Phase 1 — Foundation: COMPLETE

## Summary
All 7 waves of Phase 1 completed. Build: 0 errors, 0 warnings. Tests: 28/28 passing.

## Delivered Artifacts

### Solution Structure
- `SupportHub.slnx` — .NET 10 solution (uses .slnx format, not .sln)
- 6 projects: Domain, Application, Infrastructure, Web, Tests.Unit, Tests.Integration

### Domain (SupportHub.Domain)
- `Entities/BaseEntity.cs` — abstract, Guid PK, soft-delete fields, DateTimeOffset
- `Entities/Company.cs` — Name, Code, IsActive, Description; navigations to Divisions, UserCompanyRoles
- `Entities/Division.cs` — CompanyId FK, Name, IsActive; navigation to Company
- `Entities/ApplicationUser.cs` — AzureAdObjectId, Email, DisplayName, IsActive; navigations to UserCompanyRoles
- `Entities/UserCompanyRole.cs` — UserId FK, CompanyId FK, Role (enum); navigations to User, Company
- `Entities/AuditLogEntry.cs` — Immutable (no BaseEntity), own Guid Id, all audit fields
- `Enums/UserRole.cs` — SuperAdmin, Admin, Agent

### Application (SupportHub.Application)
- `Common/Result.cs` — Result<T> with Success/Failure factory
- `Common/PagedResult.cs` — record with Items, TotalCount, Page, PageSize
- `Interfaces/ICurrentUserService.cs` — UserId, DisplayName, Email, GetUserRolesAsync, HasAccessToCompanyAsync
- `Interfaces/ICompanyService.cs` — CRUD + pagination
- `Interfaces/IUserService.cs` — CRUD + sync + role management
- `Interfaces/IAuditService.cs` — LogAsync
- `DTOs/CompanyDtos.cs` — CompanyDto, CreateCompanyRequest, UpdateCompanyRequest
- `DTOs/DivisionDtos.cs` — DivisionDto, CreateDivisionRequest, UpdateDivisionRequest
- `DTOs/UserDtos.cs` — UserDto, UserCompanyRoleDto

### Infrastructure (SupportHub.Infrastructure)
- `Data/SupportHubDbContext.cs` — 5 DbSets, ApplyConfigurationsFromAssembly
- `Data/SupportHubDbContextFactory.cs` — IDesignTimeDbContextFactory for EF CLI
- `Data/Interceptors/AuditableEntityInterceptor.cs` — Stamps Created/Updated/Deleted fields
- `Data/Configurations/` — 5 configs (Company, Division, ApplicationUser, UserCompanyRole, AuditLogEntry)
- `Data/Migrations/20260219042733_InitialCreate.cs` — All 5 tables
- `Services/CompanyService.cs` — Full CRUD, soft-delete, audit logging
- `Services/UserService.cs` — CRUD, Azure AD upsert sync, role assignment
- `Services/AuditService.cs` — Creates AuditLogEntry with JSON-serialized values
- `DependencyInjection.cs` — AddInfrastructure() extension method

### Web (SupportHub.Web)
- `Program.cs` — Azure AD auth, authorization policies, MudBlazor, Serilog, all service registrations
- `appsettings.json` — AzureAd section, ConnectionStrings.DefaultConnection
- `Services/CurrentUserService.cs` — ICurrentUserService impl using IHttpContextAccessor + DbContext
- `Components/_Imports.razor` — Global usings including MudBlazor, Authorization, DTOs
- `Components/Layout/MainLayout.razor` — MudBlazor layout with drawer, auth display, sign-out
- `Components/Layout/NavMenu.razor` — Dashboard + SuperAdmin-gated Companies/Users nav
- `Components/Pages/Dashboard.razor` — Placeholder with metric cards + quick links
- `Components/Pages/Admin/Companies.razor` — MudDataGrid + CRUD dialogs
- `Components/Pages/Admin/CompanyFormDialog.razor` — Create/edit dialog
- `Components/Pages/Admin/Users.razor` — MudDataGrid with role chips
- `Components/Pages/Admin/UserDetail.razor` — User info + role assignment/removal
- `Controllers/CompaniesController.cs` — 5 REST endpoints
- `Controllers/UsersController.cs` — 5 REST endpoints

### Tests (SupportHub.Tests.Unit)
- `Helpers/TestDbContextFactory.cs` — Creates isolated InMemory DbContext per test
- `Services/CompanyServiceTests.cs` — 12 tests
- `Services/UserServiceTests.cs` — 11 tests
- `Services/AuditServiceTests.cs` — 5 tests

## Key Technical Decisions Made
- `.NET 10` uses `.slnx` solution format (not `.sln`)
- `AuditService` does NOT inject `IHttpContextAccessor` (Infrastructure is not Web SDK, so IpAddress = null)
- `AuditableEntityInterceptor` NOT wired into test DbContext (requires DI, InMemory tests use plain DbContext)
- `CompanyService.CreateCompanyAsync` normalizes Code to uppercase
- Global soft-delete filters applied via `HasQueryFilter(x => !x.IsDeleted)` in EF configs — tests use `IgnoreQueryFilters()` to verify deleted records

## What Phase 2 Needs from Phase 1
- All entities (Company, Division, ApplicationUser) for FK relationships
- BaseEntity (all new entities inherit from it)
- SupportHubDbContext (add new DbSets)
- DependencyInjection.cs (add new service registrations)
- ICurrentUserService, IAuditService (ticket operations need these)
- Result<T>, PagedResult<T> (service return types)
- Authorization policies and Blazor layout (ticket pages nest within existing layout)
