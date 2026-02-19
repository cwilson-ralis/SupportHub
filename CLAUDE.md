# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Ralis Support Hub is an internal multi-company support ticket system replacing fragmented tooling (Zendesk, ServiceNow, shared inboxes). It provides centralized ticket intake, routing, and reporting across multiple company entities (TLE, CSBK, CashCall, Servicing Solution, LCE, LNRES, others).

Full design: `docs/design-overview.md`

## Tech Stack

- **Runtime:** .NET 10, C# 14
- **UI:** Blazor Web App (Server interactivity) + MudBlazor
- **API:** ASP.NET Core Web API
- **Database:** SQL Server (on-prem), Entity Framework Core
- **Auth:** Azure AD via Microsoft.Identity.Web
- **Email:** Microsoft Graph API (shared mailbox polling)
- **Background Jobs:** Hangfire
- **Real-time:** SignalR
- **Logging:** Serilog (structured logging with `ILogger<T>`)
- **Attachments:** On-prem network share behind `IFileStorageService` abstraction

## Build Commands

```bash
dotnet build
dotnet test
dotnet run --project src/SupportHub.Web

# EF Core migrations
dotnet ef migrations add {Name} --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
dotnet ef database update --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
```

## Architecture

### Solution Structure

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

Single solution with shared service interfaces consumed by both Blazor UI and API controllers. Designed so API hosting can be separated later without rewrites.

### Data Model

Single SQL Server database with `CompanyId` FK isolation (not per-tenant databases). Core entities: `Company`, `Division`, `Ticket`, `TicketMessage`, `TicketAttachment`, `InternalNote`, `TicketTag`, `RoutingRule`, `CannedResponse`, `AuditLog`, `SlaPolicy`, `SlaBreachRecord`, `CustomerSatisfactionRating`, `KnowledgeBaseArticle`.

### Multi-Tenancy

Company entities are data-driven and admin-manageable (no code changes to add/remove). Company isolation **must** be enforced at the service/query layer, not just the UI.

### Email Threading

Canonical header: `X-SupportHub-TicketId`. Subject token matching is fallback only. Hangfire polls shared mailboxes on 1–2 minute intervals via Graph API.

### Routing

Admin-configurable ordered rules engine (sender domain, keyword match, form fields, tags). Unmatched tickets go to a default queue. AI-assisted classification for unstructured/email/image tickets; AI outcomes must be recorded for audit.

## Code Conventions

- Nullable reference types enabled
- File-scoped namespaces
- `record` types for DTOs
- `Result<T>` pattern for service returns — no exception throwing for business logic
- `IEntityTypeConfiguration<T>` for EF config — no data annotations on entities
- `async/await` throughout with `Async` suffix on method names
- All timestamps `DateTimeOffset` in UTC
- Soft-delete on all entities via `BaseEntity.IsDeleted` / `DeletedAt`
- Tags over rigid enums for audit/reporting agility
- Authorization enforced in API/service layer, not just UI

## Phased Build Order

1. **Foundation** — Solution structure, EF migrations, Azure AD auth, company/user management
2. **Core Ticketing** — Web form intake, ticket lifecycle, attachments, notes, canned responses, tags
3. **Email Integration** — Graph API polling, inbound/outbound, threading, AI classification
4. **Routing UI** — Rules CRUD, queue management, rules pipeline
5. **SLA & Satisfaction** — SLA policies, monitoring jobs, CSAT surveys
6. **Audit Reporting & KB** — SOX reports, dashboards, knowledge base
7. **Hardening** — Accessibility, audit validation, performance testing

## Phase Documents

Detailed task breakdowns with entity definitions, service interfaces, UI pages, and acceptance criteria:

- `docs/Phase-1-Foundation.md` — Solution scaffold, BaseEntity, Company/User entities, auth, RBAC
- `docs/Phase-2-Core-Ticketing.md` — Ticket lifecycle, messages, attachments, notes, tags, canned responses
- `docs/Phase-3-Email-Integration.md` — Graph API polling, inbound/outbound email, threading, AI stub
- `docs/Phase-4-Routing.md` — Queue/RoutingRule entities, rules engine, admin UI
- `docs/Phase-5-SLA-Satisfaction.md` — SLA policies, breach monitoring, CSAT surveys
- `docs/Phase-6-KB-Reporting.md` — Knowledge base, dashboards, audit/SOX reports, CSV export
- `docs/Phase-7-Hardening.md` — Security review, performance, SignalR, health checks, integration tests

## Multi-Agent Prompts

Agent prompts for parallel Claude Code execution. Each agent owns specific file paths:

- `prompts/orchestrator.md` — Master coordinator: decomposes phases, delegates waves, runs build gates
- `prompts/agent-backend.md` — Entities, enums, DTOs, interfaces, EF configurations, DbContext
- `prompts/agent-service.md` — Service implementations, business logic, company isolation
- `prompts/agent-ui.md` — Blazor Server pages + components with MudBlazor
- `prompts/agent-api.md` — API controllers, middleware, Swagger
- `prompts/agent-infrastructure.md` — Graph API email, Hangfire jobs, file storage, SignalR, health checks
- `prompts/agent-test.md` — Unit tests (xUnit + NSubstitute + FluentAssertions), integration tests
- `prompts/agent-reviewer.md` — Read-only code review, produces structured review reports

## Progress Tracking

- `PROGRESS.md` — Build progress tracker updated by orchestrator after each wave
- `prompts/context/` — Auto-generated context files shared between agents after each wave
