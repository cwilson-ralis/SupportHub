# CLAUDE.md — SupportHub Project Instructions

This file is read automatically by Claude Code when working in this repository.

## Project Overview

SupportHub is an internal multi-company support ticket system built with .NET 10. See `docs/Phase-0-Project-Overview.md` for full architecture details.

## Multi-Agent Build System

This project uses a multi-agent orchestration approach. The build is coordinated by an **Orchestrator Agent** that delegates to specialist sub-agents.

### Agent Prompts Location

All agent prompts are in the `prompts/` directory:

| File | Role |
|---|---|
| `prompts/orchestrator.md` | Master coordinator — read this first |
| `prompts/agent-backend.md` | Entities, DTOs, interfaces, EF config |
| `prompts/agent-service.md` | Service implementations, business logic |
| `prompts/agent-ui.md` | Blazor pages, components, layout |
| `prompts/agent-api.md` | API controllers, middleware |
| `prompts/agent-infrastructure.md` | Graph API, Hangfire, file storage |
| `prompts/agent-test.md` | Unit tests |

### Phase Documents

Build plan documents are in the `docs/` directory:

| File | Contents |
|---|---|
| `docs/Phase-0-Project-Overview.md` | Architecture, conventions, tech stack |
| `docs/Phase-1-Foundation.md` | Solution structure, DB, auth, company/user mgmt |
| `docs/Phase-2-Core-Ticketing.md` | Ticket CRUD, notes, attachments, canned responses |
| `docs/Phase-3-Email-Integration.md` | Graph API email ingestion and sending |
| `docs/Phase-4-SLA-Satisfaction.md` | SLA monitoring, breach detection, CSAT surveys |
| `docs/Phase-5-KB-Reporting.md` | Knowledge base, dashboard, reports |
| `docs/Phase-6-Polish-Hardening.md` | Audit, performance, security, docs |

### Shared Context

The `prompts/context/` directory contains extracted context files that are passed to sub-agents. These are updated after each wave by the Orchestrator.

### Progress Tracking

`PROGRESS.md` at the solution root tracks completed tasks, current wave, and blockers.

## How to Start a Build Phase

1. Read the Orchestrator prompt: `prompts/orchestrator.md`
2. Read the current phase document from `docs/`
3. Follow the wave execution protocol in the Orchestrator prompt
4. Delegate to sub-agents using their prompt files
5. Run validation gates between waves: `dotnet build && dotnet test`
6. Update `PROGRESS.md` after each wave

## Key Commands

```bash
# Build
dotnet build

# Test
dotnet test

# Run migrations
dotnet ef migrations add {Name} --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web

# Update database
dotnet ef database update --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web

# Run the app
dotnet run --project src/SupportHub.Web
```

## Conventions Quick Reference

- .NET 10, C# 14, nullable reference types enabled
- File-scoped namespaces
- `record` types for DTOs
- `Result<T>` pattern for service returns (no exception throwing for business logic)
- `IEntityTypeConfiguration<T>` for EF config (no data annotations)
- `async/await` throughout, `Async` suffix on method names
- Structured logging with `ILogger<T>` (Serilog)
- MudBlazor for UI components
- All timestamps `DateTimeOffset` in UTC
- Soft-delete on all entities via `BaseEntity.IsDeleted`
