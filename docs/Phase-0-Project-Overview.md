# Phase 0 - Project Overview & Conventions

## Purpose

This document defines the overall architecture, conventions, and shared context for the **Ralis Support Hub** support ticket system. Every phase references this document. Read it first before executing any phase.

---

## Project Summary

Ralis Support Hub is an internal, multi-company support ticket system - an alternative to Zendesk for basic ticket handling. It is used by internal employees authenticated via Azure AD. Agents can handle tickets across multiple companies. Each company has its own shared M365 mailbox for email-based ticket creation and replies.

Branding note: user-facing documentation and UI use the name **Ralis Support Hub**; existing solution/project identifiers such as `SupportHub.*` remain technical names.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor Web App (.NET 10) with Server interactivity and MudBlazor |
| Backend API | ASP.NET Core Web API (.NET 10) |
| Database | SQL Server - on-premises (company data center) |
| ORM | Entity Framework Core 10 |
| Authentication | Azure AD via `Microsoft.Identity.Web` - all users authenticate with M365 accounts |
| Authorization | Role-based + policy-based (Azure AD groups): Super Admin, Admin, Agent |
| Email | Microsoft Graph API (M365 shared mailboxes) |
| Real-time | SignalR (available through Blazor Web App Server interactivity when needed) |
| Background Jobs | Hangfire with SQL Server storage - email polling, SLA monitoring, scheduled jobs |
| File Storage | On-premises network share (behind `IFileStorageService` abstraction for future Azure Blob migration) |
| AI / Classification | Azure OpenAI (GPT-4o-mini) - image-capable model for ticket routing/classification from unstructured email submissions |
| UI Components | MudBlazor |
| CI/CD | Azure DevOps Pipelines |
| Logging | Serilog with structured logging |
| Testing | xUnit, Moq, FluentAssertions |

---

## Solution Structure

```
SupportHub/
|-- SupportHub.sln
|-- src/
|   |-- SupportHub.Web/                  # Blazor Web App (Server interactivity, frontend + hosting)
|   |   |-- Program.cs
|   |   |-- Pages/
|   |   |-- Components/
|   |   |-- Layout/
|   |   \-- wwwroot/
|   |-- SupportHub.Api/                  # ASP.NET Core Web API (can be hosted separately if needed)
|   |   |-- Controllers/
|   |   |-- Program.cs
|   |   \-- Middleware/
|   |-- SupportHub.Core/                 # Domain models, interfaces, enums, DTOs
|   |   |-- Entities/
|   |   |-- Interfaces/
|   |   |-- DTOs/
|   |   |-- Enums/
|   |   \-- Constants/
|   |-- SupportHub.Infrastructure/       # EF Core, Graph API, file storage, Hangfire jobs
|   |   |-- Data/
|   |   |   |-- AppDbContext.cs
|   |   |   |-- Configurations/          # EF Fluent API configs
|   |   |   \-- Migrations/
|   |   |-- Services/
|   |   |-- Email/
|   |   \-- Storage/
|   \-- SupportHub.ServiceDefaults/      # Shared configuration, DI registration
|-- tests/
|   |-- SupportHub.Core.Tests/
|   |-- SupportHub.Infrastructure.Tests/
|   \-- SupportHub.Web.Tests/
|-- docs/
\-- azure-pipelines.yml
```

### Project Dependency Rules

- `Core` has ZERO project references (it is the innermost layer)
- `Infrastructure` references `Core`
- `Web` references `Core` and `Infrastructure`
- `Api` references `Core` and `Infrastructure`
- `Tests` reference the project they are testing

### Runtime Hosting Model (v1)

- v1 deployment runs `SupportHub.Web` and `SupportHub.Api` in the same solution footprint.
- Blazor UI workflows call application services in-process through DI.
- API controllers use the same service interfaces for parity and remain the external integration boundary.
- `SupportHub.Api` can be split to a separate host later without changing service contracts.

---

## Coding Conventions

### General

- Use C# 14 features: primary constructors, collection expressions, file-scoped namespaces
- Use `record` types for DTOs and value objects
- Use nullable reference types (`<Nullable>enable</Nullable>`) project-wide
- Use `async/await` throughout - no `.Result` or `.Wait()` calls
- Use `ILogger<T>` for all logging via Serilog
- Use `FluentValidation` for input validation
- No magic strings - use `Constants` classes or enums

### Naming

- Interfaces: `I` prefix (e.g., `ITicketService`)
- Async methods: `Async` suffix (e.g., `GetTicketByIdAsync`)
- Private fields: `_camelCase`
- DTOs: `{Name}Dto` (e.g., `TicketDto`, `CreateTicketDto`)
- EF Configurations: `{Entity}Configuration` (e.g., `TicketConfiguration`)

### Entity Framework

- Fluent API configuration in separate `IEntityTypeConfiguration<T>` classes - never data annotations on entities
- All business entities inherit from `BaseEntity` (Id, CreatedAt, UpdatedAt, IsDeleted, DeletedAt); `AuditLog` is append-only and does not inherit from `BaseEntity`
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
- Service calls from Blazor go through injected interfaces in-process for internal UI workflows (same interfaces the API controllers use for parity)

---

## Database Design Principles

- Business tables use `int` identity primary keys; `AuditLog` uses `long` identity because it grows significantly faster
- All foreign keys have explicit indexes
- `CompanyId` on all company-scoped entities - enforced at service layer via a `CompanyContext` or query filter
- Soft-delete on all entities (retained data requirement)
- Audit columns: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on all entities
- `rowversion` / concurrency token on `Ticket` for optimistic concurrency

---

## Data Model (High Level)

### Core Entities

| Entity | Key Fields & Notes |
|---|---|
| **Company** | Name, shared mailbox address, SLA config. Must be configurable via admin UI - no code change required to add/remove entities. |
| **Division** | Optional subdivision within a company (e.g., Origination, Processing, Underwriting, Post-Closing, Funding, App Support, Tech Support). Used in backend routing and shown as Queue in the UI. |
| **UserProfile** (from Azure AD) | Role (Super Admin, Admin, Agent), assigned companies. Sourced from Azure AD; no separate user registration. |
| **UserCompanyAssignment** | Maps a user to a company. Agents only see data for assigned companies. |
| **Ticket** | Company, Queue (Division), status, priority, assigned agent, SLA timestamps (`FirstResponseAt`, `ResolvedAt`, `ClosedAt`, `SlaPausedAt`, `TotalPausedMinutes`), source (email/portal/API), system/application affected, issue type, tags, AI metadata (`AiClassified`, `AiClassificationJson`). |
| **TicketMessage** | Body, sender, direction (inbound/outbound), reply-from metadata (shared mailbox in v1), external message ID for Graph API threading. |
| **TicketAttachment** | File path, original filename, MIME type, stored on network share, linked to ticket or message. |
| **TicketTag** | Flexible tagging per ticket (e.g., `new-hire`, `termination`, `access-request`, `empower`, `salesforce`). Enables SOX audit filtering without schema changes. |
| **InternalNote** | Tied to ticket, visible only to agents. |
| **RoutingRule** | Configurable rules (domain match, keyword match, form field match) that auto-assign a ticket to a Division (displayed as Queue in the UI). Managed via admin UI - no code changes needed to adjust routing. |
| **CannedResponse** | Scoped per company or global, title, body template. |
| **KnowledgeBaseArticle** | Company-scoped, title, body (markdown), tags. |
| **SlaPolicy** | Per company and/or priority: first response target, resolution target. |
| **SlaBreachRecord** | Ticket reference, breach type, breach timestamp. |
| **SlaNotificationLog** | Tracks sent SLA breach/warning emails to prevent duplicate notifications. |
| **AuditLog** | User, action, entity type, entity ID, old/new values, timestamp. Required for SOX compliance. Append-only, never soft-deleted. |
| **CustomerSatisfactionRating** | Ticket reference, score (1-5), optional comment. Sent on ticket close. |
| **EmailProcessingLog** | Per-company polling history: emails found, tickets created, messages appended, errors. |

### Key Design Decisions

- Multi-company isolation via `CompanyId` FK on all company-scoped entities - enforced at query/service layer, not separate databases.
- `Division` is the backend routing entity; the UI presents this as Queue for agents/admins.
- `TicketTag` replaces rigid category enums to support audit use cases (`termination`, `new-hire`, `access-request`) without schema changes.
- `RoutingRule` is a first-class entity managed by admins, not hard-coded - non-developers can adjust routing logic without engineer involvement.
- AI classification (Azure OpenAI GPT-4o-mini) handles email tickets with insufficient structured data, using an image-capable model since users frequently submit screenshot-only tickets.
- Soft-delete across all business entities (`IsDeleted` + `DeletedAt`) - tickets must be retained for audit.
- All timestamps stored in UTC, displayed in local time on the frontend.

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

1. **Service Layer Pattern** - Business logic lives in `Infrastructure/Services/`, exposed via interfaces in `Core/Interfaces/`
2. **Repository Pattern is NOT used** - EF Core `DbContext` is the repository. Services use `AppDbContext` directly.
3. **Result Pattern** - Services return `Result<T>` instead of throwing exceptions
4. **Options Pattern** - Configuration via `IOptions<T>` (e.g., `EmailSettings`, `SlaSettings`, `StorageSettings`)
5. **Dependency Injection** - All services registered in a `ServiceCollectionExtensions` class per project

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

## Phased Build Order

| Phase | Name | Key Deliverables |
|---|---|---|
| **Phase 1** | Foundation | Solution structure, Azure AD auth, company/user management, EF migrations, CI/CD |
| **Phase 2** | Core Ticketing | Ticket CRUD, structured web form, file attachments, internal notes, canned responses, tagging |
| **Phase 3** | Email Integration | Graph API mailbox polling, inbound ticket creation/threading, outbound replies, AI classification |
| **Phase 4** | Rules Engine & Routing UI | Admin UI for routing rules, rule evaluation pipeline, Queue (Division) management |
| **Phase 5** | SLA & Satisfaction | SLA policy config, monitoring job, breach detection/notifications, CSAT surveys |
| **Phase 6** | Audit Reporting & Knowledge Base | SOX compliance reports, dashboard, reporting, internal KB CRUD |
| **Phase 7** | Polish & Hardening | Audit logging, performance, security hardening, documentation, production readiness |

---

## Definition of Done (per phase)

- All code compiles with zero warnings
- All new code has XML doc comments on public members
- Unit tests written for service layer logic
- EF migrations generated and tested
- No hardcoded connection strings or secrets
- All new DI registrations added to the appropriate extension method
- README updated with any new setup steps


