# Phase 1 Wave 6 — Blazor UI Pages & API Controllers

## Completed

### Layout
- `src/SupportHub.Web/Components/Layout/MainLayout.razor`: MudBlazor layout with MudAppBar, MudDrawer, NavMenu, user display, sign-out form
- `src/SupportHub.Web/Components/Layout/NavMenu.razor`: MudNavMenu with Dashboard + SuperAdmin-gated Companies/Users nav
- `src/SupportHub.Web/Components/_Imports.razor`: Added MudBlazor, Authorization, Application interfaces/DTOs, Domain enums global usings

### Pages
- `src/SupportHub.Web/Components/Pages/Dashboard.razor`: Route `/`, `[Authorize]`, placeholder metric cards + quick links
- `src/SupportHub.Web/Components/Pages/Admin/Companies.razor`: Route `/admin/companies`, `[Authorize(Policy="SuperAdmin")]`, MudDataGrid, search, create/edit/delete
- `src/SupportHub.Web/Components/Pages/Admin/CompanyFormDialog.razor`: MudDialog for create/edit companies
- `src/SupportHub.Web/Components/Pages/Admin/Users.razor`: Route `/admin/users`, `[Authorize(Policy="SuperAdmin")]`, MudDataGrid with role chips
- `src/SupportHub.Web/Components/Pages/Admin/UserDetail.razor`: Route `/admin/users/{Id:guid}`, role assignment/removal UI
- Deleted `Home.razor` (route conflict with Dashboard)

### Controllers
- `src/SupportHub.Web/Controllers/CompaniesController.cs`: GET /api/companies, GET /api/companies/{id}, POST, PUT, DELETE
- `src/SupportHub.Web/Controllers/UsersController.cs`: GET /api/users, GET /{id}, POST /sync, POST /{userId}/roles, DELETE /{userId}/roles/{companyId}/{role}
- `AssignRoleRequest` record defined at bottom of UsersController.cs

## Build Status
`dotnet build SupportHub.slnx` — 0 errors, 0 warnings ✅

## Notes for Wave 7 (Unit Tests)
- Tests project: `tests/SupportHub.Tests.Unit/`
- Use xUnit, NSubstitute, FluentAssertions (all already installed)
- Test services in isolation with mocked SupportHubDbContext via in-memory or mocked DbSets
- Key test files per phase doc:
  - `tests/SupportHub.Tests.Unit/Services/CompanyServiceTests.cs`
  - `tests/SupportHub.Tests.Unit/Services/UserServiceTests.cs`
  - `tests/SupportHub.Tests.Unit/Services/AuditServiceTests.cs`
- Use EF Core InMemory provider for DbContext in tests: add `Microsoft.EntityFrameworkCore.InMemory` package
- CompanyService depends on: SupportHubDbContext, IAuditService
- UserService depends on: SupportHubDbContext, ICurrentUserService, IAuditService
- AuditService depends on: SupportHubDbContext, ICurrentUserService
- Global soft-delete query filter is applied — tests using InMemory may need to disable it or seed IsDeleted=false
