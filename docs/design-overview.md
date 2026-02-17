Perfect — I have everything I need. Let me put together the full plan.

---

# Support Ticket System — Architecture & Build Plan

## Technology Stack

- **Frontend:** Blazor Server (.NET 10) with Mudblazor
- **Backend API:** ASP.NET Core Web API (.NET 10)
- **Database:** SQL Server (local instance)
- **Auth:** Azure AD via Microsoft.Identity.Web, role/group-based authorization
- **Email:** Microsoft Graph API (connects to M365 shared mailboxes)
- **Real-time (future):** SignalR (already built into Blazor Server)
- **File Storage:** Local file system or network share for v1 (abstracting behind an interface so you can swap to Azure Blob later)
- **Background Jobs:** Hangfire (email polling, SLA monitoring, scheduled reports)
- **CI/CD:** Azure DevOps Pipelines
- **ORM:** Entity Framework Core
- **Reporting:** Embedded dashboards using a lightweight library (e.g., Radzen components or MudBlazor charts)

---

## Data Model (High Level)

**Core Entities:**

- **Company** — name, shared mailbox address, SLA config, branding basics
- **Division/Department** — optional subdivision within a company
- **User (from Azure AD)** — role (Super Admin, Admin, Agent), assigned companies
- **Ticket** — company, status, priority, assigned agent, SLA timestamps, source (email/portal/API), tags
- **TicketMessage** — body, sender, direction (inbound/outbound), reply-from preference (system vs agent), timestamps
- **TicketAttachment** — file path, original filename, MIME type, linked to ticket or message
- **InternalNote** — tied to ticket, visible only to agents
- **CannedResponse** — scoped per company or global, title, body template
- **KnowledgeBaseArticle** — company-scoped, title, body (markdown), tags
- **SLAPolicy** — per company and/or priority, first response target, resolution target
- **SLABreach** — ticket reference, breach type, breach timestamp
- **CustomerSatisfactionRating** — ticket reference, score, optional comment

**Key Design Decisions:**

- Multi-company isolation via a `CompanyId` foreign key on most entities, enforced at the query/service layer — not separate databases. This keeps it simple while allowing cross-company agent access and super admin reporting.
- Soft-delete across the board (`IsDeleted` + `DeletedAt`) since tickets must be retained.
- All timestamps in UTC, displayed in local time on the frontend.

---

## Email Flow

1. **Inbound:** Hangfire job polls each shared mailbox via Microsoft Graph API on a short interval (e.g., every 1–2 minutes). New emails either create a ticket or append a message to an existing ticket (matched by subject line threading or a ticket ID token in the subject/headers).
2. **Outbound:** When an agent replies, the system sends via Graph API from either the shared mailbox or the agent's personal mailbox, based on their selection. The sent message is recorded as a TicketMessage.

---

## SLA Engine

- A Hangfire recurring job runs every few minutes, checking open tickets against their applicable SLA policy.
- Tracks two clocks: time to first response and time to resolution.
- When a threshold is approaching or breached, the system creates an SLABreach record and can send notification emails or surface warnings in the UI.
- Business hours support can be added later; v1 can use simple elapsed time.

---

## Phased Build Order

### Phase 1 — Foundation (Weeks 1–3)
- Solution structure: Blazor Server project, API project, shared class library, EF Core data project
- Azure AD authentication and role-based authorization (Super Admin, Admin, Agent)
- Company and user management
- Database schema and migrations
- Basic CI/CD pipeline in Azure DevOps

### Phase 2 — Core Ticketing (Weeks 4–6)
- Ticket CRUD (create, assign, update status/priority, close)
- Ticket list views with filtering (by company, status, agent, priority)
- Internal notes on tickets
- File attachment upload and storage
- Canned responses (CRUD + insert into reply)

### Phase 3 — Email Integration (Weeks 7–8)
- Microsoft Graph API connection to shared mailboxes
- Inbound email polling and ticket creation/threading
- Outbound replies with sender selection (shared mailbox vs agent)
- Email-to-ticket matching logic

### Phase 4 — SLA & Satisfaction (Weeks 9–10)
- SLA policy configuration per company/priority
- SLA monitoring background job
- SLA status indicators on ticket list and detail views
- Customer satisfaction survey (sent on ticket close, rating stored)

### Phase 5 — Knowledge Base & Reporting (Weeks 11–12)
- Internal KB article CRUD with search
- Agent can reference/link KB articles in ticket replies
- Reporting dashboard: open/closed ticket counts over time, average first response time, average resolution time, SLA breach rate, unassigned/aging tickets, tickets by company/agent/priority

### Phase 6 — Polish & Hardening (Week 13+)
- UI refinements, loading states, error handling
- Audit logging
- Performance testing
- Documentation
- Production deployment and monitoring setup

---

## Risks & Considerations

- **Graph API permissions** — you'll need admin consent for Mail.ReadWrite on the shared mailboxes. Worth getting IT involved early.
- **Email threading** — matching replies to existing tickets can get tricky. A ticket ID token injected into email headers (e.g., `X-Ticket-Id`) is more reliable than subject line matching alone.
- **SLA clock pausing** — v1 uses simple elapsed time, but eventually you'll want to pause the clock when waiting on the customer. Worth designing the data model to support this even if you don't build it yet.
- **File storage** — abstracting behind an `IFileStorageService` interface from day one means swapping to Azure Blob or S3 later is trivial.