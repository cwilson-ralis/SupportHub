# Phase 1 - Foundation (Weeks 1-3)

> **Prerequisites:** Read `Phase-0-Project-Overview.md` first. All conventions, naming, and structure defined there apply here.

---

## Objective

Set up the solution structure, database, authentication, authorization, and basic company/user management. At the end of this phase, an authenticated user can log in, see which companies they have access to, and Super Admins can manage companies and user assignments.

---

## Task 1.1 - Create Solution Structure

### Instructions

1. Create a new .NET 10 solution named `SupportHub` using the structure defined in Phase 0.
2. Create the following projects:
 - `SupportHub.Web` - Blazor Web App (.NET 10) with Server interactivity (do not use the legacy standalone Blazor Server template)
 - `SupportHub.Api` - ASP.NET Core Web API (controllers, not minimal API)
 - `SupportHub.Core` - Class Library
 - `SupportHub.Infrastructure` - Class Library
 - `SupportHub.ServiceDefaults` - Class Library
3. Set up project references per the dependency rules in Phase 0.
4. Add the following NuGet packages:

| Project | Packages |
|---|---|
| `Core` | `FluentValidation` |
| `Infrastructure` | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.Graph`, `Microsoft.Identity.Web`, `Hangfire.Core`, `Hangfire.SqlServer`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.File` |
| `Web` | `MudBlazor`, `Microsoft.Identity.Web.UI` |
| `Api` | `Microsoft.Identity.Web`, `Asp.Versioning.Mvc`, `Asp.Versioning.Mvc.ApiExplorer`, `Swashbuckle.AspNetCore` |

5. Configure `Directory.Build.props` at the solution root:

```xml
<Project>
 <PropertyGroup>
 <TargetFramework>net10.0</TargetFramework>
 <Nullable>enable</Nullable>
 <ImplicitUsings>enable</ImplicitUsings>
 <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
 </PropertyGroup>
</Project>
```

6. Add a `.editorconfig` with C# defaults and `dotnet_diagnostic` rules.
7. Add a `.gitignore` for .NET.

---

## Task 1.2 - Define Base Entity and Core Enums

### Instructions

1. In `SupportHub.Core/Entities/`, create `BaseEntity`:

```csharp
namespace SupportHub.Core.Entities;

public abstract class BaseEntity
{
 public int Id { get; set; }
 public DateTimeOffset CreatedAt { get; set; }
 public string? CreatedBy { get; set; }
 public DateTimeOffset UpdatedAt { get; set; }
 public string? UpdatedBy { get; set; }
 public bool IsDeleted { get; set; }
 public DateTimeOffset? DeletedAt { get; set; }
}
```

2. In `SupportHub.Core/Enums/`, create the following enums:

```csharp
public enum AppRole { SuperAdmin, Admin, Agent }
public enum TicketStatus { New, Open, AwaitingCustomer, AwaitingAgent, OnHold, Resolved, Closed }
public enum TicketPriority { Low, Medium, High, Urgent }
public enum TicketSource { Email, Portal, Api }
public enum MessageDirection { Inbound, Outbound }
public enum ReplySenderType { SharedMailbox } // v1 scope; personal mailbox mode is post-v1
public enum SlaBreachType { FirstResponse, Resolution }
public enum IssueType { AccessRequest, ErrorOrBug, HowToQuestion, Training, NewHire, Termination, Other }
public enum RoutingConditionType { SenderDomain, SubjectKeyword, BodyKeyword, FormSystemApplication, FormIssueType }
```

3. In `SupportHub.Core/`, create a `Result<T>` type for the Result pattern:

```csharp
namespace SupportHub.Core;

public class Result<T>
{
 public bool IsSuccess { get; }
 public T? Value { get; }
 public string? Error { get; }

 private Result(T value) { IsSuccess = true; Value = value; }
 private Result(string error) { IsSuccess = false; Error = error; }

 public static Result<T> Success(T value) => new(value);
 public static Result<T> Failure(string error) => new(error);
}
```

---

## Task 1.3 - Define Core Entities

### Instructions

Create the following entities in `SupportHub.Core/Entities/`. Business entities inherit from `BaseEntity` unless noted otherwise. `AuditLog` is append-only and does **not** inherit from `BaseEntity`. Use navigation properties where appropriate. Do NOT use data annotations - all configuration will be Fluent API.

1. **Company**
 - `string Name` (required, max 200)
 - `string SharedMailboxAddress` (required, max 320 - max email length)
 - `string? Description`
 - `bool IsActive`
 - Navigation: `ICollection<Division> Divisions`, `ICollection<Ticket> Tickets`, `ICollection<UserCompanyAssignment> UserAssignments`, `ICollection<SlaPolicy> SlaPolicies`, `ICollection<CannedResponse> CannedResponses`, `ICollection<KnowledgeBaseArticle> KnowledgeBaseArticles`, `ICollection<RoutingRule> RoutingRules`

2. **Division** (routing unit within a company - e.g., Tech Support, App Support, New Hire/Termination)
 - `int CompanyId` (FK)
 - `string Name` (required, max 200)
 - `string? Description`
 - `bool IsActive`
 - Navigation: `Company Company`, `ICollection<Ticket> Tickets`, `ICollection<RoutingRule> RoutingRules`

3. **UserProfile** (represents an Azure AD user's local profile)
 - `string AzureAdObjectId` (required, max 36 - GUID format)
 - `string DisplayName` (required, max 200)
 - `string Email` (required, max 320)
 - `AppRole Role`
 - `bool IsActive`
 - Navigation: `ICollection<UserCompanyAssignment> CompanyAssignments`

4. **UserCompanyAssignment**
 - `int UserProfileId` (FK)
 - `int CompanyId` (FK)
 - Navigation: `UserProfile UserProfile`, `Company Company`

5. **Ticket**
 - `int CompanyId` (FK)
 - `int? DivisionId` (FK -> Division, nullable - null means unrouted/General queue)
 - `string Subject` (required, max 500)
 - `TicketStatus Status`
 - `TicketPriority Priority`
 - `TicketSource Source`
 - `string? SystemApplication` (max 200 - e.g., Empower, Salesforce, Outlook, Hardware; from web form dropdown)
 - `IssueType? IssueType` (enum - from web form dropdown; null for email-submitted tickets before classification)
 - `int? AssignedAgentId` (FK -> UserProfile, nullable)
 - `string RequesterEmail` (required, max 320)
 - `string RequesterName` (required, max 200)
 - `DateTimeOffset? FirstResponseAt`
 - `DateTimeOffset? ResolvedAt`
 - `DateTimeOffset? ClosedAt`
 - `DateTimeOffset? SlaPausedAt` (set when SLA is paused, e.g., awaiting customer)
 - `int TotalPausedMinutes` (accumulated paused minutes for future SLA logic)
 - `bool AiClassified` (whether AI classification was applied)
 - `string? AiClassificationJson` (raw AI output JSON for audit/tuning)
 - `byte[] RowVersion` (concurrency token)
 - Navigation: `Company Company`, `Division? Division`, `UserProfile? AssignedAgent`, `ICollection<TicketMessage> Messages`, `ICollection<TicketAttachment> Attachments`, `ICollection<TicketTag> Tags`, `ICollection<InternalNote> InternalNotes`, `ICollection<SlaBreachRecord> SlaBreaches`, `CustomerSatisfactionRating? SatisfactionRating`

6. **TicketTag** (flexible tagging - enables SOX audit filtering without schema changes)
 - `int TicketId` (FK)
 - `string Tag` (required, max 100 - e.g., `new-hire`, `termination`, `access-request`, `empower`, `salesforce`)
 - Navigation: `Ticket Ticket`
 - Index: `TicketId + Tag` (unique composite)

8. **TicketMessage**
 - `int TicketId` (FK)
 - `string Body` (required, nvarchar(max))
 - `string SenderEmail` (required, max 320)
 - `string SenderName` (required, max 200)
 - `MessageDirection Direction`
 - `ReplySenderType? ReplySenderType` (only for outbound)
 - `string? ExternalMessageId` (max 500, Graph API message ID for deduplication)
 - Navigation: `Ticket Ticket`, `ICollection<TicketAttachment> Attachments`

9. **TicketAttachment**
 - `int? TicketId` (FK, nullable)
 - `int? TicketMessageId` (FK, nullable)
 - `string OriginalFileName` (required, max 500)
 - `string StoredFileName` (required, max 500)
 - `string ContentType` (required, max 200)
 - `long FileSizeBytes`
 - Navigation: `Ticket? Ticket`, `TicketMessage? TicketMessage`

10. **InternalNote**
 - `int TicketId` (FK)
 - `int AuthorId` (FK -> UserProfile)
 - `string Body` (required, nvarchar(max))
 - Navigation: `Ticket Ticket`, `UserProfile Author`

11. **CannedResponse**
 - `int? CompanyId` (FK, nullable - null means global)
 - `string Title` (required, max 200)
 - `string Body` (required, nvarchar(max))
 - `int SortOrder`
 - Navigation: `Company? Company`

12. **KnowledgeBaseArticle**
 - `int CompanyId` (FK)
 - `int AuthorId` (FK -> UserProfile)
 - `string Title` (required, max 500)
 - `string Body` (required, nvarchar(max), stored as Markdown)
 - `string? Tags` (max 1000, comma-separated for v1)
 - `bool IsPublished`
 - Navigation: `Company Company`, `UserProfile Author`

13. **RoutingRule** (admin-configurable rules that assign tickets to a Division (displayed as Queue in the UI) without code changes)
 - `int CompanyId` (FK)
 - `string Name` (required, max 200)
 - `bool IsEnabled`
 - `int SortOrder`
 - `RoutingConditionType ConditionType` (enum: SenderDomain, SubjectKeyword, BodyKeyword, FormSystemApplication, FormIssueType)
 - `string ConditionValue` (required, max 500 - the value to match, e.g., `@tle.com`, `Empower`)
 - `int? TargetDivisionId` (FK -> Division, nullable - where to route if matched; null = default/General)
 - `string? AutoTag` (max 100, optional tag to apply when rule matches, e.g., `termination`)
 - Navigation: `Company Company`, `Division? TargetDivision`

14. **SlaPolicy**
 - `int CompanyId` (FK)
 - `TicketPriority Priority`
 - `int FirstResponseMinutes`
 - `int ResolutionMinutes`
 - Navigation: `Company Company`
 - Unique constraint: `CompanyId + Priority`

15. **SlaBreachRecord**
 - `int TicketId` (FK)
 - `int SlaPolicyId` (FK)
 - `SlaBreachType BreachType`
 - `DateTimeOffset BreachedAt`
 - Navigation: `Ticket Ticket`, `SlaPolicy SlaPolicy`

16. **CustomerSatisfactionRating**
 - `int TicketId` (FK, unique)
 - `int Score` (1-5)
 - `string? Comment` (max 2000)
 - Navigation: `Ticket Ticket`

17. **AuditLog** (append-only audit trail; required for SOX reporting from early phases)
 - `long Id` (PK, identity)
 - `DateTimeOffset Timestamp`
 - `string UserId` (required, max 36 - Azure AD Object ID)
 - `string UserDisplayName` (required, max 200)
 - `string Action` (required, max 200)
 - `string EntityType` (required, max 100)
 - `int? EntityId`
 - `string? OldValues` (nvarchar(max), JSON)
 - `string? NewValues` (nvarchar(max), JSON)
 - `string? AdditionalInfo` (nvarchar(max), JSON)
 - `string? IpAddress` (max 64)
 - Notes: append-only, never soft-deleted, no global query filter

---

## Task 1.4 - Configure EF Core and Database

### Instructions

1. In `SupportHub.Infrastructure/Data/`, create `AppDbContext`:
 - Register all `DbSet<T>` properties (including `DbSet<AuditLog>`)
 - Override `OnModelCreating` to apply all configurations from the assembly: `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);`
 - Override `SaveChangesAsync` to automatically set `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` (inject `ICurrentUserService` to get the user identity)
 - Add a global query filter on `BaseEntity` for soft-delete: `.HasQueryFilter(e => !e.IsDeleted)`

2. Create `IEntityTypeConfiguration<T>` classes for every entity in `Infrastructure/Data/Configurations/`:
 - Set all `MaxLength` constraints
 - Set required/optional
 - Configure `RowVersion` as `IsRowVersion()` on Ticket
 - Configure unique index on `UserProfile.AzureAdObjectId`
 - Configure unique composite index on `UserCompanyAssignment(UserProfileId, CompanyId)`
 - Configure unique composite index on `SlaPolicy(CompanyId, Priority)`
 - Configure unique index on `CustomerSatisfactionRating.TicketId`
 - Configure unique composite index on `TicketTag(TicketId, Tag)`
 - Add index on `TicketTag.Tag` (for filtering by tag across tickets)
 - Add indexes on all foreign keys
 - Add composite index `IX_Ticket_CompanyId_Status` for filtered ticket list queries
 - Add index `IX_Ticket_AssignedAgentId` for agent workload queries
 - Add index `IX_RoutingRule_CompanyId_SortOrder` for ordered rule evaluation
 - Configure `AuditLog` as append-only (no soft-delete/query filter behavior)
 - Add index `IX_AuditLog_Timestamp` (descending for recent-first queries)
 - Add index `IX_AuditLog_EntityType_EntityId` for entity history lookups
 - Add index `IX_AuditLog_UserId` for user activity history
 - Configure `DeleteBehavior.Restrict` on all relationships (soft-delete means we never cascade hard-delete)

3. In `SupportHub.Core/Interfaces/`, create `ICurrentUserService`:

```csharp
public interface ICurrentUserService
{
 string? UserId { get; } // Azure AD Object ID
 string? DisplayName { get; }
 string? Email { get; }
 AppRole? Role { get; }
 bool IsSuperAdmin { get; }
}
```

4. Implement `CurrentUserService` in `Infrastructure/Services/` using `IHttpContextAccessor` to read claims from the Azure AD token.

5. Generate the initial EF migration:

```bash
dotnet ef migrations add InitialCreate --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
```

---

## Task 1.5 - Configure Authentication and Authorization

### Instructions

1. Register an App Registration in Azure AD (document the manual steps in `docs/AzureAdSetup.md`):
 - App type: Web
 - Redirect URI: `https://localhost:{port}/signin-oidc`
 - API permissions: `User.Read`, `Mail.ReadWrite`, `Mail.Send` (for later phases)
 - Create App Roles: `SuperAdmin`, `Admin`, `Agent`
 - Map Azure AD groups to app roles

2. In `SupportHub.Web/Program.cs`:
 - Add `Microsoft.Identity.Web` with OpenID Connect
 - Add authorization policies:

```csharp
builder.Services.AddAuthorizationBuilder()
 .AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"))
 .AddPolicy("AdminOrAbove", p => p.RequireRole("SuperAdmin", "Admin"))
 .AddPolicy("AgentOrAbove", p => p.RequireRole("SuperAdmin", "Admin", "Agent"));
```

 - Add a custom `IAuthorizationHandler` for `CompanyAccessRequirement` that checks the user's company assignments (or bypasses for SuperAdmin)
 - Configure `CascadingAuthenticationState` in `App.razor`

3. In `SupportHub.Api/Program.cs`:
 - Add JWT Bearer authentication via `Microsoft.Identity.Web`
 - Add the same authorization policies
 - Add API versioning (v1)
 - Add Swagger with Azure AD auth support

---

## Task 1.6 - Company Management

### Instructions

1. Create `ICompanyService` in `Core/Interfaces/`:

```csharp
public interface ICompanyService
{
 Task<Result<List<CompanyDto>>> GetAllAsync();
 Task<Result<CompanyDto>> GetByIdAsync(int id);
 Task<Result<CompanyDto>> CreateAsync(CreateCompanyDto dto);
 Task<Result<CompanyDto>> UpdateAsync(int id, UpdateCompanyDto dto);
 Task<Result<bool>> DeactivateAsync(int id); // soft-delete
}
```

2. Create DTOs in `Core/DTOs/`:
 - `CompanyDto` (read)
 - `CreateCompanyDto` (write - Name, SharedMailboxAddress, Description)
 - `UpdateCompanyDto` (write)

3. Implement `CompanyService` in `Infrastructure/Services/`. Validate inputs with `FluentValidation`.

4. Create Blazor pages in `Web/Pages/Admin/`:
 - `Companies.razor` - List all companies (SuperAdmin only). Use `MudDataGrid` with search/filter.
 - `CompanyDetail.razor` - Create/edit a company. Use `MudForm` with validation.

5. Create API controller `CompaniesController` in `Api/Controllers/v1/`:
 - Standard CRUD endpoints
 - Authorize with `[Authorize(Policy = "SuperAdmin")]`

---

## Task 1.7 - User & Assignment Management

### Instructions

1. Create `IUserService` in `Core/Interfaces/`:

```csharp
public interface IUserService
{
 Task<Result<UserProfileDto>> GetOrCreateFromAzureAdAsync(string objectId, string displayName, string email);
 Task<Result<List<UserProfileDto>>> GetAllAsync();
 Task<Result<UserProfileDto>> UpdateRoleAsync(int userId, AppRole role);
 Task<Result<bool>> AssignToCompanyAsync(int userId, int companyId);
 Task<Result<bool>> RemoveFromCompanyAsync(int userId, int companyId);
 Task<Result<List<CompanyDto>>> GetUserCompaniesAsync(int userId);
}
```

2. Create DTOs: `UserProfileDto`, `UpdateUserRoleDto`, `AssignUserCompanyDto`

3. Implement `UserService`. The `GetOrCreateFromAzureAdAsync` method should:
 - Check if a `UserProfile` exists by `AzureAdObjectId`
 - If not, create one with default role `Agent`
 - This is called on first login (from the `CurrentUserService` or a middleware)

4. Create Blazor pages:
 - `Users.razor` - List all users (AdminOrAbove). Show role, assigned companies.
 - `UserDetail.razor` - Edit role, manage company assignments.

5. Create `UsersController` in the API.

---

## Task 1.8 - Middleware, Logging, and Error Handling

### Instructions

1. Configure Serilog in `Program.cs` for both Web and Api projects:
 - Console sink (development)
 - File sink with rolling daily files (production)
 - Enrich with `CorrelationId`, `UserId`

2. Create global exception handling middleware for the API:
 - Catch unhandled exceptions
 - Log with Serilog
 - Return `ProblemDetails` response

3. In Blazor, create a shared `ErrorBoundary` component that logs errors and shows a user-friendly message.

4. Add health check endpoint: `/health` that verifies database connectivity.

---

## Task 1.9 - CI/CD Pipeline

### Instructions

1. Create `azure-pipelines.yml` at the solution root with:
 - Trigger on `main` and `develop` branches
 - Steps: Restore -> Build -> Test -> Publish
 - Use `.NET 10` SDK
 - Run `dotnet test` with code coverage
 - Publish artifacts for the Web and Api projects

2. Document local development setup in `docs/LocalSetup.md`:
 - Prerequisites: .NET 10 SDK, SQL Server, Azure AD app registration values
 - SQL Server connection string
 - How to run migrations
 - How to start the app

---

## Acceptance Criteria for Phase 1

- [ ] Solution builds with zero warnings
- [ ] Database is created via EF migrations with all tables
- [ ] A user can log in via Azure AD and see the app
- [ ] SuperAdmin can create/edit/deactivate companies
- [ ] SuperAdmin/Admin can view users, change roles, assign users to companies
- [ ] Unauthorized users are redirected or see a 403
- [ ] Health check returns 200 when DB is reachable
- [ ] CI pipeline builds and runs tests successfully
- [ ] All services have unit tests for happy path and key error cases

