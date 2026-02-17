# Phase 0 — Project Overview & Conventions

## Purpose

This document defines the overall architecture, conventions, and shared context for the **SupportHub** support ticket system. Every phase references this document. Read it first before executing any phase.

---

## Project Summary

SupportHub is an internal, multi-company support ticket system — an alternative to Zendesk for basic ticket handling. It is used by internal employees authenticated via Azure AD. Agents can handle tickets across multiple companies. Each company has its own shared M365 mailbox for email-based ticket creation and replies.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor Server (.NET 10) with MudBlazor |
| Backend API | ASP.NET Core Web API (.NET 10) |
| Database | SQL Server (local instance) |
| ORM | Entity Framework Core 10 |
| Authentication | Azure AD via `Microsoft.Identity.Web` |
| Authorization | Role-based + policy-based (Azure AD groups) |
| Email | Microsoft Graph API (M365 shared mailboxes) |
| Background Jobs | Hangfire with SQL Server storage |
| File Storage | Local file system (behind `IFileStorageService` abstraction) |
| UI Components | MudBlazor |
| CI/CD | Azure DevOps Pipelines |
| Logging | Serilog with structured logging |
| Testing | xUnit, Moq, FluentAssertions |

---

## Solution Structure

```
SupportHub/
├── SupportHub.sln
├── src/
│   ├── SupportHub.Web/                  # Blazor Server app (frontend + hosting)
│   │   ├── Program.cs
│   │   ├── Pages/
│   │   ├── Components/
│   │   ├── Layout/
│   │   └── wwwroot/
│   ├── SupportHub.Api/                  # ASP.NET Core Web API (optional separate host)
│   │   ├── Controllers/
│   │   ├── Program.cs
│   │   └── Middleware/
│   ├── SupportHub.Core/                 # Domain models, interfaces, enums, DTOs
│   │   ├── Entities/
│   │   ├── Interfaces/
│   │   ├── DTOs/
│   │   ├── Enums/
│   │   └── Constants/
│   ├── SupportHub.Infrastructure/       # EF Core, Graph API, file storage, Hangfire jobs
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/          # EF Fluent API configs
│   │   │   └── Migrations/
│   │   ├── Services/
│   │   ├── Email/
│   │   └── Storage/
│   └── SupportHub.ServiceDefaults/      # Shared configuration, DI registration
├── tests/
│   ├── SupportHub.Core.Tests/
│   ├── SupportHub.Infrastructure.Tests/
│   └── SupportHub.Web.Tests/
├── docs/
└── azure-pipelines.yml
```

### Project Dependency Rules

- `Core` has ZERO project references (it is the innermost layer)
- `Infrastructure` references `Core`
- `Web` references `Core` and `Infrastructure`
- `Api` references `Core` and `Infrastructure`
- `Tests` reference the project they are testing

---

## Coding Conventions

### General

- Use C# 12 features: primary constructors, collection expressions, file-scoped namespaces
- Use `record` types for DTOs and value objects
- Use nullable reference types (`<Nullable>enable</Nullable>`) project-wide
- Use `async/await` throughout — no `.Result` or `.Wait()` calls
- Use `ILogger<T>` for all logging via Serilog
- Use `FluentValidation` for input validation
- No magic strings — use `Constants` classes or enums

### Naming

- Interfaces: `I` prefix (e.g., `ITicketService`)
- Async methods: `Async` suffix (e.g., `GetTicketByIdAsync`)
- Private fields: `_camelCase`
- DTOs: `{Name}Dto` (e.g., `TicketDto`, `CreateTicketDto`)
- EF Configurations: `{Entity}Configuration` (e.g., `TicketConfiguration`)

### Entity Framework

- Fluent API configuration in separate `IEntityTypeConfiguration<T>` classes — never data annotations on entities
- All entities inherit from `BaseEntity` (Id, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
- Use global query filters for soft-delete (`entity.IsDeleted == false`)
- Use `DateTimeOffset` for all timestamps (stored as UTC)
- All string columns have explicit `MaxLength` set

### API

- Use the Result pattern (`Result<T>`) instead of throwing exceptions for business logic failures
- Return `ProblemDetails` for error responses
- Use API versioning from v1

### Blazor

- Use MudBlazor component library
- Component files: `{Name}.razor` with code-behind `{Name}.razor.cs`
- Use `CascadingAuthenticationState` for auth context
- Service calls from Blazor go through injected interfaces (same interfaces the API controllers use)

---

## Database Design Principles

- All tables use `int` identity primary keys (BIGINT not needed at this scale)
- All foreign keys have explicit indexes
- `CompanyId` on all company-scoped entities — enforced at service layer via a `CompanyContext` or query filter
- Soft-delete on all entities (retained data requirement)
- Audit columns: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on all entities
- `rowversion` / concurrency token on `Ticket` for optimistic concurrency

---

## Authentication & Authorization Model

| Azure AD Group | App Role | Permissions |
|---|---|---|
| `SupportHub-SuperAdmins` | `SuperAdmin` | All companies, all config, all reports |
| `SupportHub-Admins` | `Admin` | Manage assigned companies, agents, SLA, canned responses |
| `SupportHub-Agents` | `Agent` | Work tickets for assigned companies |

- Users are mapped to companies via a `UserCompanyAssignment` table
- Authorization policies enforce company-level access (e.g., `CanAccessCompany(companyId)`)
- SuperAdmins bypass company-level checks

---

## Key Architectural Patterns

1. **Service Layer Pattern** — Business logic lives in `Infrastructure/Services/`, exposed via interfaces in `Core/Interfaces/`
2. **Repository Pattern is NOT used** — EF Core `DbContext` is the repository. Services use `AppDbContext` directly.
3. **Result Pattern** — Services return `Result<T>` instead of throwing exceptions
4. **Options Pattern** — Configuration via `IOptions<T>` (e.g., `EmailSettings`, `SlaSettings`, `StorageSettings`)
5. **Dependency Injection** — All services registered in a `ServiceCollectionExtensions` class per project

---

## Environment Configuration

```json
// appsettings.json structure (example)
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "ClientSecret": "<client-secret>"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=SupportHub;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "EmailSettings": {
    "PollingIntervalSeconds": 60,
    "TicketIdHeaderName": "X-SupportHub-TicketId"
  },
  "StorageSettings": {
    "BasePath": "C:\\SupportHub\\Attachments",
    "MaxFileSizeMb": 25
  },
  "HangfireSettings": {
    "DashboardPath": "/hangfire"
  }
}
```

---

## Definition of Done (per phase)

- All code compiles with zero warnings
- All new code has XML doc comments on public members
- Unit tests written for service layer logic
- EF migrations generated and tested
- No hardcoded connection strings or secrets
- All new DI registrations added to the appropriate extension method
- README updated with any new setup steps
