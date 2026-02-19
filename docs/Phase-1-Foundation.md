# Phase 1 — Foundation

## Overview

Solution scaffold, base abstractions, authentication, and company/user management. This phase establishes the full project structure, shared patterns (Result\<T\>, BaseEntity, soft-delete), Entity Framework Core configuration, Azure AD authentication, and the first CRUD screens for companies and users. Every subsequent phase builds on the artifacts delivered here.

## Prerequisites

| Prerequisite | Notes |
|---|---|
| .NET 10 SDK | Required for C# 14 features and Blazor Server |
| SQL Server instance | On-prem; any edition (Developer is fine for local work) |
| Azure AD app registration | OpenID Connect; redirect URI for Blazor Server (`https://localhost:{port}/signin-oidc`) |
| Node.js (optional) | Only if MudBlazor tooling or CSS isolation build steps require it |

## Solution Structure

```
SupportHub.sln
src/
  SupportHub.Domain/           — Entities, enums, value objects
  SupportHub.Application/      — DTOs (records), service interfaces, Result<T>, PagedResult<T>
  SupportHub.Infrastructure/   — EF DbContext, configurations, service implementations, email, jobs, storage
  SupportHub.Web/              — Blazor pages/components, API controllers, middleware, Program.cs
tests/
  SupportHub.Tests.Unit/       — xUnit + NSubstitute + FluentAssertions
  SupportHub.Tests.Integration/ — Integration tests (Phase 7)
```

### Project Details and Dependencies

#### 1. SupportHub.Domain

**Responsibility:** Pure domain layer. Entities, enums, value objects. No infrastructure dependencies.

**NuGet packages:** None (no external dependencies).

**Project references:** None.

---

#### 2. SupportHub.Application

**Responsibility:** Application contracts — DTOs (record types), service interfaces, shared abstractions (`Result<T>`, `PagedResult<T>`). This is the boundary that both the Web and Infrastructure projects depend on.

**NuGet packages:** None (keep this project dependency-free).

**Project references:**
- `SupportHub.Domain`

---

#### 3. SupportHub.Infrastructure

**Responsibility:** All external concerns — EF Core DbContext and entity configurations, service implementations, Graph API integration, Hangfire job definitions, file storage.

**NuGet packages:**
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Tools` (design-time)
- `Microsoft.Identity.Web` (for downstream API calls if needed)
- `Microsoft.Graph` (Phase 3, but project exists now)
- `Hangfire.SqlServer` (Phase 3+, but project exists now)
- `Serilog.Extensions.Logging`

**Project references:**
- `SupportHub.Domain`
- `SupportHub.Application`

---

#### 4. SupportHub.Web

**Responsibility:** Blazor Server host, pages/components, API controllers, middleware, `Program.cs` startup configuration, `ICurrentUserService` implementation.

**NuGet packages:**
- `MudBlazor`
- `Microsoft.Identity.Web`
- `Microsoft.Identity.Web.UI`
- `Microsoft.EntityFrameworkCore.Design` (for EF CLI tooling via startup project)
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`

**Project references:**
- `SupportHub.Domain`
- `SupportHub.Application`
- `SupportHub.Infrastructure`

---

#### 5. SupportHub.Tests.Unit

**Responsibility:** Fast, isolated unit tests for service logic, validation, and mapping.

**NuGet packages:**
- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`
- `NSubstitute`
- `FluentAssertions`

**Project references:**
- `SupportHub.Domain`
- `SupportHub.Application`
- `SupportHub.Infrastructure`

---

#### 6. SupportHub.Tests.Integration

**Responsibility:** End-to-end tests against a real database (Phase 7 scope, but project is created now).

**NuGet packages:**
- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`
- `Microsoft.AspNetCore.Mvc.Testing`
- `FluentAssertions`
- `Testcontainers.MsSql` (optional, for containerized SQL in CI)

**Project references:**
- `SupportHub.Web`
- `SupportHub.Infrastructure`

---

## Wave 1 — Domain Core & Shared Abstractions

### BaseEntity

All entities (except AuditLogEntry) inherit from `BaseEntity`. This provides identity, audit stamps, and soft-delete fields.

```csharp
namespace SupportHub.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

**Key decisions:**
- `Guid` PKs are generated client-side via `Guid.NewGuid()` — no database-generated identity columns.
- All timestamps are `DateTimeOffset` stored in UTC.
- `CreatedBy` / `UpdatedBy` / `DeletedBy` store the user's display name or Azure AD object ID (populated automatically by the SaveChanges interceptor).
- Soft-delete is the default deletion strategy. A global query filter ensures deleted records are excluded unless explicitly requested.

---

### Result\<T\>

Defined in `SupportHub.Application.Common`. Service methods return `Result<T>` instead of throwing exceptions for business logic failures.

```csharp
namespace SupportHub.Application.Common;

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

**Usage pattern:**
```csharp
public async Task<Result<CompanyDto>> CreateCompanyAsync(CreateCompanyRequest request, CancellationToken ct)
{
    if (await _context.Companies.AnyAsync(c => c.Code == request.Code, ct))
        return Result<CompanyDto>.Failure("A company with this code already exists.");

    // ... create and save ...
    return Result<CompanyDto>.Success(dto);
}
```

---

### PagedResult\<T\>

A record type for paginated query responses.

```csharp
namespace SupportHub.Application.Common;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

---

### Enums

Defined in `SupportHub.Domain.Enums`:

```csharp
namespace SupportHub.Domain.Enums;

public enum UserRole
{
    SuperAdmin,
    Admin,
    Agent
}
```

**Notes:**
- `UserRole` is stored as a string in the database (`HasConversion<string>()`) for readability in queries and reports.
- Additional enums (TicketStatus, TicketPriority, etc.) will be added in Phase 2.

---

## Wave 2 — Entities

### Company

**File:** `src/SupportHub.Domain/Entities/Company.cs`

| Property | Type | Constraints |
|---|---|---|
| Id | Guid (PK) | Inherited from BaseEntity |
| Name | string | Required, max 200 |
| Code | string | Required, max 50, unique |
| IsActive | bool | Default `true` |
| Description | string? | Max 1000 |
| *(BaseEntity fields)* | | CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, DeletedBy |

**Navigations:**
- `ICollection<Division> Divisions`
- `ICollection<UserCompanyRole> UserCompanyRoles`

```csharp
namespace SupportHub.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public ICollection<Division> Divisions { get; set; } = [];
    public ICollection<UserCompanyRole> UserCompanyRoles { get; set; } = [];
}
```

---

### Division

**File:** `src/SupportHub.Domain/Entities/Division.cs`

| Property | Type | Constraints |
|---|---|---|
| Id | Guid (PK) | Inherited from BaseEntity |
| CompanyId | Guid (FK -> Company) | Required |
| Name | string | Required, max 200 |
| IsActive | bool | Default `true` |
| *(BaseEntity fields)* | | |

**Navigations:**
- `Company Company`

```csharp
namespace SupportHub.Domain.Entities;

public class Division : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
}
```

---

### ApplicationUser

**File:** `src/SupportHub.Domain/Entities/ApplicationUser.cs`

| Property | Type | Constraints |
|---|---|---|
| Id | Guid (PK) | Inherited from BaseEntity |
| AzureAdObjectId | string | Required, unique |
| Email | string | Required, max 256, unique |
| DisplayName | string | Required, max 200 |
| IsActive | bool | Default `true` |
| *(BaseEntity fields)* | | |

**Navigations:**
- `ICollection<UserCompanyRole> UserCompanyRoles`

```csharp
namespace SupportHub.Domain.Entities;

public class ApplicationUser : BaseEntity
{
    public string AzureAdObjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<UserCompanyRole> UserCompanyRoles { get; set; } = [];
}
```

---

### UserCompanyRole

**File:** `src/SupportHub.Domain/Entities/UserCompanyRole.cs`

| Property | Type | Constraints |
|---|---|---|
| Id | Guid (PK) | Inherited from BaseEntity |
| UserId | Guid (FK -> ApplicationUser) | Required |
| CompanyId | Guid (FK -> Company) | Required |
| Role | UserRole | Required (stored as string) |
| *(BaseEntity fields)* | | |

**Navigations:**
- `ApplicationUser User`
- `Company Company`

```csharp
namespace SupportHub.Domain.Entities;

using SupportHub.Domain.Enums;

public class UserCompanyRole : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public UserRole Role { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
```

---

### AuditLogEntry

**File:** `src/SupportHub.Domain/Entities/AuditLogEntry.cs`

> **Important:** `AuditLogEntry` does **not** inherit from `BaseEntity`. Audit records are immutable — they are never updated or soft-deleted.

| Property | Type | Constraints |
|---|---|---|
| Id | Guid (PK) | `Guid.NewGuid()` |
| Timestamp | DateTimeOffset | Required |
| UserId | string? | Azure AD object ID or null for system actions |
| UserDisplayName | string? | |
| Action | string | Required (e.g. "Create", "Update", "Delete") |
| EntityType | string | Required (e.g. "Company", "Ticket") |
| EntityId | string | Required |
| OldValues | string? | JSON-serialized previous state |
| NewValues | string? | JSON-serialized new state |
| IpAddress | string? | |
| AdditionalData | string? | JSON-serialized extra context |

```csharp
namespace SupportHub.Domain.Entities;

public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; }
    public string? UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? AdditionalData { get; set; }
}
```

---

## Wave 3 — EF Core Configuration & DbContext

### SupportHubDbContext

**File:** `src/SupportHub.Infrastructure/Data/SupportHubDbContext.cs`

```csharp
namespace SupportHub.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using SupportHub.Domain.Entities;

public class SupportHubDbContext : DbContext
{
    public SupportHubDbContext(DbContextOptions<SupportHubDbContext> options)
        : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<UserCompanyRole> UserCompanyRoles => Set<UserCompanyRole>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportHubDbContext).Assembly);
    }
}
```

**Key behaviors:**
- `ApplyConfigurationsFromAssembly` discovers all `IEntityTypeConfiguration<T>` implementations in the Infrastructure assembly.
- Global query filter for soft-delete is applied in each entity configuration that inherits from `BaseEntity`.

### SaveChanges Interceptor

**File:** `src/SupportHub.Infrastructure/Data/AuditableEntityInterceptor.cs`

An EF Core `SaveChangesInterceptor` that:
1. Finds all `Added` entities inheriting `BaseEntity` and sets `CreatedAt = DateTimeOffset.UtcNow`, `CreatedBy = currentUser`.
2. Finds all `Modified` entities inheriting `BaseEntity` and sets `UpdatedAt = DateTimeOffset.UtcNow`, `UpdatedBy = currentUser`.
3. Intercepts soft-delete: when `IsDeleted` changes to `true`, sets `DeletedAt = DateTimeOffset.UtcNow`, `DeletedBy = currentUser`.

The interceptor depends on `ICurrentUserService` (resolved via DI) to get the current user identity.

---

### Entity Configurations

All configurations live in `src/SupportHub.Infrastructure/Data/Configurations/`.

#### CompanyConfiguration

**File:** `CompanyConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("IX_Companies_Code");

        builder.HasIndex(c => c.Name)
            .HasDatabaseName("IX_Companies_Name");

        // Soft-delete global query filter
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
```

---

#### DivisionConfiguration

**File:** `DivisionConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable("Divisions");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        // FK to Company — restrict delete (do not cascade)
        builder.HasOne(d => d.Company)
            .WithMany(c => c.Divisions)
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: one division name per company
        builder.HasIndex(d => new { d.CompanyId, d.Name })
            .IsUnique()
            .HasDatabaseName("IX_Divisions_CompanyId_Name");

        // Soft-delete global query filter
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
```

---

#### ApplicationUserConfiguration

**File:** `ApplicationUserConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("ApplicationUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.AzureAdObjectId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(u => u.AzureAdObjectId)
            .IsUnique()
            .HasDatabaseName("IX_ApplicationUsers_AzureAdObjectId");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_ApplicationUsers_Email");

        // Soft-delete global query filter
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
```

---

#### UserCompanyRoleConfiguration

**File:** `UserCompanyRoleConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class UserCompanyRoleConfiguration : IEntityTypeConfiguration<UserCompanyRole>
{
    public void Configure(EntityTypeBuilder<UserCompanyRole> builder)
    {
        builder.ToTable("UserCompanyRoles");

        builder.HasKey(r => r.Id);

        // Store enum as string
        builder.Property(r => r.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // FK to ApplicationUser
        builder.HasOne(r => r.User)
            .WithMany(u => u.UserCompanyRoles)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to Company
        builder.HasOne(r => r.Company)
            .WithMany(c => c.UserCompanyRoles)
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: one role per user per company
        builder.HasIndex(r => new { r.UserId, r.CompanyId, r.Role })
            .IsUnique()
            .HasDatabaseName("IX_UserCompanyRoles_UserId_CompanyId_Role");

        // Soft-delete global query filter
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
```

---

#### AuditLogEntryConfiguration

**File:** `AuditLogEntryConfiguration.cs`

```csharp
namespace SupportHub.Infrastructure.Data.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportHub.Domain.Entities;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.UserDisplayName)
            .HasMaxLength(200);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45); // IPv6 max length

        // Query indexes
        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("IX_AuditLogEntries_EntityType_EntityId");

        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("IX_AuditLogEntries_Timestamp");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditLogEntries_UserId");

        // No soft-delete filter — audit logs are immutable
    }
}
```

---

### Initial Migration

After all entity configurations are in place, create and apply the initial migration:

```bash
# Create the migration
dotnet ef migrations add InitialCreate \
    --project src/SupportHub.Infrastructure \
    --startup-project src/SupportHub.Web

# Apply the migration to the database
dotnet ef database update \
    --project src/SupportHub.Infrastructure \
    --startup-project src/SupportHub.Web
```

**Verify the following tables are created:**
- `Companies`
- `Divisions`
- `ApplicationUsers`
- `UserCompanyRoles`
- `AuditLogEntries`

Plus the EF Core `__EFMigrationsHistory` table.

---

## Wave 4 — Authentication & Authorization

### Azure AD Setup

#### App Registration

1. Register an application in Azure AD.
2. Set the redirect URI to `https://localhost:{port}/signin-oidc`.
3. Add a client secret (store in user secrets or Azure Key Vault, never in `appsettings.json`).
4. Configure the following API permissions: `User.Read` (delegated).

#### Program.cs Configuration

```csharp
// Authentication — Azure AD via OpenID Connect
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

#### appsettings.json Structure

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "CallbackPath": "/signin-oidc"
  }
}
```

> Client secrets must be stored in user secrets (`dotnet user-secrets`) for development or Azure Key Vault for deployed environments.

---

### Authorization Policies

Defined in `Program.cs` (or a dedicated `AuthorizationPolicies` static class):

```csharp
builder.Services.AddAuthorization(options =>
{
    // Require authentication for all endpoints by default
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // SuperAdmin — must have SuperAdmin role in any company
    options.AddPolicy("SuperAdmin", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "SuperAdmin")));

    // Admin — must have Admin or SuperAdmin role (company context checked in service layer)
    options.AddPolicy("Admin", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "Admin") ||
            context.User.HasClaim("role", "SuperAdmin")));

    // Agent — must have Agent, Admin, or SuperAdmin role
    options.AddPolicy("Agent", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("role", "Agent") ||
            context.User.HasClaim("role", "Admin") ||
            context.User.HasClaim("role", "SuperAdmin")));
});
```

> **Note:** The policy definitions above use a simplified claims-based check. In practice, the `ICurrentUserService` and database-backed roles are the authoritative source. Policies will call into `ICurrentUserService` via a custom `IAuthorizationHandler` to verify company-scoped access. The exact implementation will be refined during coding.

---

### ICurrentUserService

**File:** `src/SupportHub.Application/Interfaces/ICurrentUserService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

using SupportHub.Domain.Entities;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? DisplayName { get; }
    string? Email { get; }
    Task<IReadOnlyList<UserCompanyRole>> GetUserRolesAsync(CancellationToken ct = default);
    Task<bool> HasAccessToCompanyAsync(Guid companyId, CancellationToken ct = default);
}
```

#### Implementation (Web project)

**File:** `src/SupportHub.Web/Services/CurrentUserService.cs`

The implementation:
1. Reads `ClaimsPrincipal` from `IHttpContextAccessor`.
2. Extracts `UserId` from the `oid` (Azure AD object ID) claim.
3. Extracts `DisplayName` from the `name` claim.
4. Extracts `Email` from the `preferred_username` or `email` claim.
5. Queries `UserCompanyRoles` from the database (cached per-request) for `GetUserRolesAsync`.
6. `HasAccessToCompanyAsync` checks whether the user has any role for the given company, or is a SuperAdmin (which grants access to all companies).

```csharp
namespace SupportHub.Web.Services;

using Microsoft.EntityFrameworkCore;
using SupportHub.Application.Interfaces;
using SupportHub.Domain.Entities;
using SupportHub.Infrastructure.Data;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SupportHubDbContext _dbContext;
    private IReadOnlyList<UserCompanyRole>? _cachedRoles;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        SupportHubDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

    public string? DisplayName =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("name")?.Value;

    public string? Email =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst("preferred_username")?.Value;

    public async Task<IReadOnlyList<UserCompanyRole>> GetUserRolesAsync(
        CancellationToken ct = default)
    {
        if (_cachedRoles is not null)
            return _cachedRoles;

        if (UserId is null)
            return [];

        var user = await _dbContext.ApplicationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AzureAdObjectId == UserId, ct);

        if (user is null)
            return [];

        _cachedRoles = await _dbContext.UserCompanyRoles
            .AsNoTracking()
            .Where(r => r.UserId == user.Id)
            .ToListAsync(ct);

        return _cachedRoles;
    }

    public async Task<bool> HasAccessToCompanyAsync(
        Guid companyId, CancellationToken ct = default)
    {
        var roles = await GetUserRolesAsync(ct);

        // SuperAdmin has access to all companies
        if (roles.Any(r => r.Role == Domain.Enums.UserRole.SuperAdmin))
            return true;

        return roles.Any(r => r.CompanyId == companyId);
    }
}
```

**Registration in Program.cs:**
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
```

---

## Wave 5 — Service Interfaces & Implementations

### DTOs (Application Project)

All DTOs are immutable `record` types defined in `src/SupportHub.Application/DTOs/`.

```csharp
namespace SupportHub.Application.DTOs;

// --- Company DTOs ---

public record CompanyDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    string? Description,
    DateTimeOffset CreatedAt,
    int DivisionCount);

public record CreateCompanyRequest(
    string Name,
    string Code,
    string? Description);

public record UpdateCompanyRequest(
    string Name,
    string Code,
    bool IsActive,
    string? Description);

// --- Division DTOs ---

public record DivisionDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CreateDivisionRequest(
    string Name);

public record UpdateDivisionRequest(
    string Name,
    bool IsActive);

// --- User DTOs ---

public record UserDto(
    Guid Id,
    string AzureAdObjectId,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<UserCompanyRoleDto> Roles);

public record UserCompanyRoleDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Role);
```

---

### ICompanyService

**File:** `src/SupportHub.Application/Interfaces/ICompanyService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;

public interface ICompanyService
{
    Task<Result<PagedResult<CompanyDto>>> GetCompaniesAsync(
        int page, int pageSize, CancellationToken ct = default);

    Task<Result<CompanyDto>> GetCompanyByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<Result<CompanyDto>> CreateCompanyAsync(
        CreateCompanyRequest request, CancellationToken ct = default);

    Task<Result<CompanyDto>> UpdateCompanyAsync(
        Guid id, UpdateCompanyRequest request, CancellationToken ct = default);

    Task<Result<bool>> DeleteCompanyAsync(
        Guid id, CancellationToken ct = default);
}
```

---

### IUserService

**File:** `src/SupportHub.Application/Interfaces/IUserService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

using SupportHub.Application.Common;
using SupportHub.Application.DTOs;
using SupportHub.Domain.Enums;

public interface IUserService
{
    Task<Result<PagedResult<UserDto>>> GetUsersAsync(
        int page, int pageSize, CancellationToken ct = default);

    Task<Result<UserDto>> GetUserByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<Result<UserDto>> SyncUserFromAzureAdAsync(
        string azureAdObjectId, CancellationToken ct = default);

    Task<Result<bool>> AssignRoleAsync(
        Guid userId, Guid companyId, UserRole role, CancellationToken ct = default);

    Task<Result<bool>> RemoveRoleAsync(
        Guid userId, Guid companyId, UserRole role, CancellationToken ct = default);
}
```

---

### IAuditService

**File:** `src/SupportHub.Application/Interfaces/IAuditService.cs`

```csharp
namespace SupportHub.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken ct = default);
}
```

---

### Service Implementations (Infrastructure Project)

All implementations live in `src/SupportHub.Infrastructure/Services/`.

#### CompanyService

**File:** `src/SupportHub.Infrastructure/Services/CompanyService.cs`

**Responsibilities:**
- **GetCompaniesAsync:** Paginated query with total count. Maps to `CompanyDto` including `DivisionCount`.
- **GetCompanyByIdAsync:** Single lookup by ID. Returns `Failure` if not found.
- **CreateCompanyAsync:** Validates `Code` uniqueness (case-insensitive). Creates entity, saves, logs audit event.
- **UpdateCompanyAsync:** Validates `Code` uniqueness (excluding self). Updates fields, saves, logs audit event with old and new values.
- **DeleteCompanyAsync:** Soft-delete — sets `IsDeleted = true`, `DeletedAt`, `DeletedBy`. Logs audit event. Does not hard-delete.

**Validation rules:**
- `Name` is required and must not exceed 200 characters.
- `Code` is required, must not exceed 50 characters, and must be unique across non-deleted companies.
- Returns `Result<T>.Failure(...)` for all validation errors (no exceptions).

---

#### UserService

**File:** `src/SupportHub.Infrastructure/Services/UserService.cs`

**Responsibilities:**
- **GetUsersAsync:** Paginated query. Includes `UserCompanyRoles` with company names.
- **GetUserByIdAsync:** Single lookup with roles.
- **SyncUserFromAzureAdAsync:** Looks up user by `AzureAdObjectId`. If not found, creates a new `ApplicationUser` record using the Azure AD claims from the current context. If found, updates `Email` and `DisplayName` if they have changed. Logs audit event.
- **AssignRoleAsync:** Validates that user and company exist. Checks for duplicate role assignment. Creates `UserCompanyRole`. Logs audit event.
- **RemoveRoleAsync:** Finds and soft-deletes the matching `UserCompanyRole`. Logs audit event.

---

#### AuditService

**File:** `src/SupportHub.Infrastructure/Services/AuditService.cs`

**Responsibilities:**
- Creates a new `AuditLogEntry` with:
  - `Timestamp = DateTimeOffset.UtcNow`
  - `UserId` and `UserDisplayName` from `ICurrentUserService`
  - `OldValues` and `NewValues` serialized to JSON via `System.Text.Json`
  - `IpAddress` from `IHttpContextAccessor` (if available)
- Saves directly to the database (audit writes should not fail silently — log and rethrow if the save fails).

---

### Service Registration

**File:** `src/SupportHub.Infrastructure/DependencyInjection.cs`

```csharp
namespace SupportHub.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportHub.Application.Interfaces;
using SupportHub.Infrastructure.Data;
using SupportHub.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SupportHubDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
```

---

## Wave 6 — Blazor UI Pages

### Layout

**File:** `src/SupportHub.Web/Components/Layout/MainLayout.razor`

The main layout uses MudBlazor's layout components:

- **MudLayout** — Provides the overall page structure.
- **MudAppBar** — Top bar with application title, company selector, and user menu.
  - **Company selector:** A `MudSelect<Guid>` dropdown in the app bar that lists all companies the current user has access to. The selected company is stored in a scoped `CompanyContext` service (cascading parameter) so all child components can access it.
  - **User menu:** Displays the current user's name with a logout option.
- **MudDrawer / MudNavMenu** — Side navigation with links to Admin pages and Dashboard.
- **MudMainContent** — The body content area where routed pages render.

### Theme

Configure a MudBlazor `MudTheme` with the Ralis corporate color palette (or a sensible default) in `Program.cs` or a `ThemeProvider` component.

---

### Pages

#### 1. Company List — `/admin/companies`

**File:** `src/SupportHub.Web/Components/Pages/Admin/Companies.razor`

- Protected by `[Authorize(Policy = "SuperAdmin")]`.
- Displays a `MudDataGrid<CompanyDto>` with server-side pagination.
- Columns: Name, Code, IsActive (chip), Division Count, Created At, Actions (Edit / Delete).
- **Search:** Text filter on Name or Code.
- **Add Company:** Opens a `MudDialog` with the `CreateCompanyRequest` form (Name, Code, Description).
- **Edit Company:** Opens a `MudDialog` with the `UpdateCompanyRequest` form.
- **Delete Company:** Confirmation dialog, then calls `DeleteCompanyAsync` (soft-delete).

#### 2. Company Detail/Edit — `/admin/companies/{id:guid}`

**File:** `src/SupportHub.Web/Components/Pages/Admin/CompanyDetail.razor`

- Protected by `[Authorize(Policy = "SuperAdmin")]`.
- Displays company information with inline editing.
- **Divisions section:** A `MudDataGrid<DivisionDto>` showing all divisions for this company.
  - Add Division dialog, Edit Division dialog, Delete Division (soft-delete).
- **User Roles section:** Shows users assigned to this company and their roles.

#### 3. User List — `/admin/users`

**File:** `src/SupportHub.Web/Components/Pages/Admin/Users.razor`

- Protected by `[Authorize(Policy = "SuperAdmin")]`.
- Displays a `MudDataGrid<UserDto>` with server-side pagination.
- Columns: Display Name, Email, IsActive (chip), Roles (list of `MudChip` per company/role), Actions.
- **Search:** Text filter on DisplayName or Email.

#### 4. User Detail — `/admin/users/{id:guid}`

**File:** `src/SupportHub.Web/Components/Pages/Admin/UserDetail.razor`

- Protected by `[Authorize(Policy = "SuperAdmin")]`.
- Displays user information (Display Name, Email, Azure AD Object ID, IsActive).
- **Company Role Assignments section:**
  - Shows current role assignments in a `MudTable`.
  - **Assign Role:** A form with Company dropdown + Role dropdown, calls `AssignRoleAsync`.
  - **Remove Role:** Confirmation dialog, calls `RemoveRoleAsync`.

#### 5. Dashboard — `/`

**File:** `src/SupportHub.Web/Components/Pages/Dashboard.razor`

- Protected by `[Authorize]` (any authenticated user).
- Placeholder page with:
  - Welcome message: "Welcome to Ralis Support Hub, {DisplayName}".
  - Brief summary cards (placeholders for actual metrics from Phase 6).
  - Quick links to admin pages (if authorized).

---

### API Controllers

Thin wrappers over service interfaces. All controllers use `[ApiController]` and return appropriate HTTP status codes based on `Result<T>`.

#### CompaniesController

**File:** `src/SupportHub.Web/Controllers/CompaniesController.cs`

```
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "SuperAdmin")]
```

| Method | Route | Service Call |
|---|---|---|
| GET | `/api/companies?page=1&pageSize=20` | `GetCompaniesAsync` |
| GET | `/api/companies/{id}` | `GetCompanyByIdAsync` |
| POST | `/api/companies` | `CreateCompanyAsync` |
| PUT | `/api/companies/{id}` | `UpdateCompanyAsync` |
| DELETE | `/api/companies/{id}` | `DeleteCompanyAsync` |

**Response mapping:**
- `Result.IsSuccess == true` -> `200 OK` (or `201 Created` for POST) with `Value`.
- `Result.IsSuccess == false` -> `400 Bad Request` with `Error` message.
- Entity not found -> `404 Not Found`.

#### UsersController

**File:** `src/SupportHub.Web/Controllers/UsersController.cs`

```
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "SuperAdmin")]
```

| Method | Route | Service Call |
|---|---|---|
| GET | `/api/users?page=1&pageSize=20` | `GetUsersAsync` |
| GET | `/api/users/{id}` | `GetUserByIdAsync` |
| POST | `/api/users/sync` | `SyncUserFromAzureAdAsync` |
| POST | `/api/users/{userId}/roles` | `AssignRoleAsync` |
| DELETE | `/api/users/{userId}/roles/{companyId}/{role}` | `RemoveRoleAsync` |

---

## Wave 7 — Tests

### Unit Tests

**Project:** `SupportHub.Tests.Unit`

All tests use:
- **xUnit** as the test framework.
- **NSubstitute** for mocking `SupportHubDbContext` (via interface or `DbContextOptions` with in-memory provider), `ICurrentUserService`, and `IAuditService`.
- **FluentAssertions** for expressive assertions.

#### CompanyServiceTests

**File:** `tests/SupportHub.Tests.Unit/Services/CompanyServiceTests.cs`

| Test | Description |
|---|---|
| `GetCompaniesAsync_ReturnsPagedResult` | Returns correct page of companies with total count. |
| `GetCompanyByIdAsync_WhenExists_ReturnsCompany` | Returns the company when found. |
| `GetCompanyByIdAsync_WhenNotFound_ReturnsFailure` | Returns `Result.Failure` when company does not exist. |
| `CreateCompanyAsync_WithValidData_CreatesAndReturns` | Creates company, saves to DB, returns DTO. |
| `CreateCompanyAsync_WithDuplicateCode_ReturnsFailure` | Returns failure when Code already exists. |
| `UpdateCompanyAsync_WithValidData_UpdatesAndReturns` | Updates fields and returns updated DTO. |
| `UpdateCompanyAsync_WhenNotFound_ReturnsFailure` | Returns failure for nonexistent company. |
| `DeleteCompanyAsync_SoftDeletesSetsIsDeleted` | Sets `IsDeleted = true`, `DeletedAt`, `DeletedBy`. |
| `DeleteCompanyAsync_WhenNotFound_ReturnsFailure` | Returns failure for nonexistent company. |
| `CreateCompanyAsync_LogsAuditEvent` | Verifies `IAuditService.LogAsync` is called. |
| `UpdateCompanyAsync_LogsAuditEventWithOldAndNewValues` | Verifies audit log includes old and new state. |
| `DeleteCompanyAsync_LogsAuditEvent` | Verifies audit log for delete action. |

#### UserServiceTests

**File:** `tests/SupportHub.Tests.Unit/Services/UserServiceTests.cs`

| Test | Description |
|---|---|
| `GetUsersAsync_ReturnsPagedResult` | Returns correct page with roles included. |
| `GetUserByIdAsync_WhenExists_ReturnsUserWithRoles` | Returns user DTO with role assignments. |
| `GetUserByIdAsync_WhenNotFound_ReturnsFailure` | Returns failure for nonexistent user. |
| `SyncUserFromAzureAdAsync_NewUser_CreatesUser` | Creates `ApplicationUser` when not found in DB. |
| `SyncUserFromAzureAdAsync_ExistingUser_UpdatesFields` | Updates `Email`/`DisplayName` if changed. |
| `AssignRoleAsync_ValidRequest_CreatesRole` | Creates `UserCompanyRole` record. |
| `AssignRoleAsync_DuplicateRole_ReturnsFailure` | Returns failure when role already assigned. |
| `AssignRoleAsync_InvalidUser_ReturnsFailure` | Returns failure when user does not exist. |
| `AssignRoleAsync_InvalidCompany_ReturnsFailure` | Returns failure when company does not exist. |
| `RemoveRoleAsync_ExistingRole_SoftDeletes` | Soft-deletes the `UserCompanyRole`. |
| `RemoveRoleAsync_NotFound_ReturnsFailure` | Returns failure when role assignment not found. |

#### AuditServiceTests

**File:** `tests/SupportHub.Tests.Unit/Services/AuditServiceTests.cs`

| Test | Description |
|---|---|
| `LogAsync_CreatesAuditLogEntry` | Verifies entry is created and saved. |
| `LogAsync_PopulatesUserInfoFromCurrentUserService` | Verifies `UserId` and `UserDisplayName` come from `ICurrentUserService`. |
| `LogAsync_SerializesOldAndNewValuesToJson` | Verifies JSON serialization of values. |
| `LogAsync_SetsTimestampToUtcNow` | Verifies `Timestamp` is approximately `DateTimeOffset.UtcNow`. |

---

## Acceptance Criteria

- [ ] Solution builds with `dotnet build` — zero errors, zero warnings
- [ ] All 6 projects created with correct inter-project references
- [ ] EF migration creates all Phase 1 tables (Companies, Divisions, ApplicationUsers, UserCompanyRoles, AuditLogEntries)
- [ ] Azure AD login redirects and authenticates successfully
- [ ] Company CRUD works end-to-end (create, read, update, soft-delete)
- [ ] Division management within companies works (create, read, update, soft-delete)
- [ ] User sync from Azure AD creates/updates ApplicationUser records
- [ ] Role assignment/removal works for all UserRole values
- [ ] Global query filter excludes soft-deleted records by default
- [ ] SaveChanges interceptor auto-populates CreatedAt/UpdatedAt/CreatedBy/UpdatedBy
- [ ] Audit log entries created for all create, update, and delete operations
- [ ] All unit tests pass with `dotnet test`
- [ ] API controllers return appropriate HTTP status codes
- [ ] Blazor pages render correctly with MudBlazor components
- [ ] Company selector in app bar filters data by selected company

## Dependencies

None. This is the foundation phase — all other phases depend on it.

## Next Phase

**Phase 2 — Core Ticketing** depends on the following artifacts from Phase 1:
- `BaseEntity` (all ticket entities inherit from it)
- `Company` and `Division` entities (tickets belong to a company/division)
- `ApplicationUser` entity (tickets are assigned to agents)
- `SupportHubDbContext` (new entities and configurations will be added)
- `ICurrentUserService` (ticket operations require user context)
- `Result<T>` and `PagedResult<T>` (service return types)
- `IAuditService` (ticket lifecycle events must be audited)
- Blazor layout with company selector (ticket pages nest within the existing layout)
- Authentication and authorization infrastructure (ticket pages are protected)
