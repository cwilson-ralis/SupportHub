# Ralis Support Hub - Tech Support Platform
## Architecture & Build Plan
*Updated per Kickoff Meeting*

---

## Project Overview

Ralis Support Hub is an internally developed support platform built to replace the fragmented collection of third-party tools (Zendesk, ServiceNow, email inboxes) currently used across company entities. The goal is to unify all support operations into a single, centralized platform that the internal IT/dev team owns, can customize, and can extend over time.

The platform will serve as a hub for:
- Support ticketing (Phase 2)
- Knowledge base and self-service (Phase 6+)
- Cross-team operational visibility (ongoing)

---

## Project Context & Background

### Supported Entities (Companies)

The system must support all entities under the company umbrella. The number of entities is larger than initially assumed (~7-8 confirmed, with more possible). Entities currently known to require support:

- TLE 
- CSBK
- CashCall
- Servicing Solution
- LCE
- LNRES
- Additional marketing/other entities (TBD)

The Company entity list must be data-driven and maintainable by admins without code changes, so entities can be added or removed easily as the business evolves.

### Current Pain Points

- Support requests come in through multiple disconnected channels: individual email inboxes, Teams pings, and ServiceNow - with no unified view.
- Tickets often arrive with vague or missing information (no system identified, just a screenshot, no error detail), requiring costly follow-up.
- Operations (tech support) and development (app support) are siloed - neither team has visibility into the other's workload or ticket history.
- SOX/audit requests for ticket histories (e.g., all terminations or access requests in a date range) must be manually assembled today.

---

## Technology Stack

| Component | Technology / Decision |
|---|---|
| Frontend | Blazor Web App (.NET 10) with Server interactivity and MudBlazor |
| Backend API | ASP.NET Core Web API (.NET 10) |
| Database | SQL Server - on-premises (company data center). Cloud storage was evaluated and ruled out due to cost, especially for file attachments. |
| Authentication | Azure AD via Microsoft.Identity.Web. All users authenticate with their Microsoft identity (M365 accounts). |
| Authorization | Role/group-based (Super Admin, Admin, Agent) enforced at the API/service layer. |
| Email | Microsoft Graph API, connecting to M365 shared mailboxes. |
| Real-time | SignalR (available through Blazor Web App Server interactivity when needed). |
| File Storage | On-premises network share for v1, abstracted behind `IFileStorageService` to allow future migration to Azure Blob. |
| Background Jobs | Hangfire - email polling, SLA monitoring, scheduled reports. |
| AI / Classification | Lightweight LLM (e.g., Azure OpenAI GPT-4o-mini) for intelligent ticket routing and classification. Image-capable model required to handle screenshot-only submissions. |
| ORM | Entity Framework Core |
| Reporting | Embedded dashboards (MudBlazor Charts or Radzen components) |
| CI/CD | Azure DevOps Pipelines |
| Project Tracking | Azure DevOps (Ralis) - dedicated project board for Ralis Support Hub |

---

## Runtime Model (v1)

- v1 deployment runs Web and API in the same solution footprint.
- Blazor pages use application services in-process for internal UI workflows.
- API controllers use the same service interfaces for parity and remain the external integration boundary.
- API can be split to a separate host later without changing service contracts.

---

## Data Model (High Level)

### Core Entities

| Entity | Key Fields & Notes |
|---|---|
| **Company** | Name, shared mailbox address, SLA config, branding. Must be configurable via admin UI - no code change required to add/remove entities. |
| **Division** | Optional subdivision within a company (e.g., Origination, Processing, Underwriting, Post-Closing, Funding, App Support, Tech Support). Used in backend routing and shown as Queue in the UI. |
| **User** (from Azure AD) | Role (Super Admin, Admin, Agent), assigned companies. Sourced from Azure AD; no separate user registration. |
| **Ticket** | Company, status, priority, assigned agent, SLA timestamps (`FirstResponseAt`, `ResolvedAt`, `ClosedAt`, `SlaPausedAt`, `TotalPausedMinutes`), source (email/portal/API), tags, routing division (shown as Queue in UI), AI metadata (`AiClassified`, `AiClassificationJson`). |
| **TicketMessage** | Body, sender, direction (inbound/outbound), reply-from metadata (shared mailbox in v1), timestamps. Supports rich text and embedded images. |
| **TicketAttachment** | File path, original filename, MIME type, stored on network share, linked to ticket or message. |
| **InternalNote** | Tied to ticket, visible only to agents. |
| **TicketTag** | Flexible tagging per ticket (e.g., `new-hire`, `termination`, `access-request`, `empower`, `salesforce`). Enables audit filtering. |
| **RoutingRule** | Configurable rules (domain match, keyword match, entity match) that auto-assign a ticket to a division (displayed as Queue in the UI). Managed via admin UI. |
| **CannedResponse** | Scoped per company or global, title, body template. |
| **KnowledgeBaseArticle** | Company-scoped, title, body (markdown), tags. Future: RAG integration with SharePoint. |
| **SlaPolicy** | Per company and/or priority: first response target, resolution target. v1 uses simple elapsed time. |
| **SlaBreachRecord** | Ticket reference, breach type, breach timestamp. |
| **AuditLog** | User, action, entity type, entity ID, timestamp. Required for SOX compliance reporting. |
| **CustomerSatisfactionRating** | Ticket reference, score, optional comment. Sent on ticket close. |

### Key Design Decisions

- Multi-company isolation via `CompanyId` foreign key on most entities, enforced at the query/service layer - not separate databases. Keeps the schema simple while enabling cross-company admin views and super admin reporting.
- Soft-delete across the board (`IsDeleted` + `DeletedAt`) - tickets must be retained for audit.
- All timestamps stored in UTC, displayed in local time on the frontend.
- Tags (`TicketTag`) replace rigid category enums to support audit use cases (terminations, new hires, access requests) without schema changes.
- `RoutingRule` is a first-class entity managed by admins, not hard-coded - non-developers (Kim, Jason) can adjust routing logic without engineer involvement.

---

## Ticket Submission Channels

A key requirement surfaced in the kickoff: email-only submission produces poor-quality data (vague descriptions, screenshot-only messages with no text context). The team agreed to prioritize a structured web form as the primary intake channel, while still supporting inbound email.

### Web Form (Primary - Preferred Channel)

Users log in with their Microsoft identity and submit tickets through a structured portal form. The form enforces minimum required fields before submission, dramatically improving routing accuracy:

- Entity / Company (dropdown)
- System / Application affected (dropdown: Empower, Salesforce, Outlook, Hardware, Other...)
- Issue type (dropdown: Access Request, Error/Bug, How-to Question, Training, New Hire, Termination, Other)
- Subject
- Description (required, minimum character count)
- Attachments (images and files - highlighted as critical by the team for screenshot-based issues)

Structured fields feed directly into the routing rules engine, reducing reliance on AI classification for routine submissions.

### Inbound Email (Secondary Channel)

Hangfire polls each company shared mailbox via Microsoft Graph API every 1-2 minutes. Inbound emails either create a new ticket or append a message to an existing thread (matched by ticket ID token in headers or subject line).

Email-submitted tickets with insufficient routing data will fall into a General queue and require manual triage. A future enhancement could prompt the end user via auto-reply to use the web form instead.

### Future

- API endpoint for programmatic ticket creation (webhooks from other internal systems).

---

## Ticket Routing & Rules Engine

Automatic, configurable routing is one of the most important features of the system. Routing must work without developer intervention once configured.

### Rules Engine

A configurable rules engine evaluates each incoming ticket against an ordered list of rules. Rules are managed through the admin UI. Rule conditions can include:

- Sender email domain (e.g., `@tle.com` -> TLE entity)
- Keyword match in subject or body (e.g., "Empower" -> App Support queue)
- Selected entity from web form
- Selected system/application from web form
- Issue type tag (e.g., `termination`, `new-hire` -> specific queue + auto-tag)

If no rule matches, the ticket falls into a configurable default queue (General). Admins can reorder, enable/disable, and add rules at any time.

### AI-Assisted Classification

For email-submitted tickets lacking structured fields, an AI classification step (using GPT-4o-mini via Azure OpenAI) will analyze the subject, body, and any attached images to suggest a routing queue and relevant tags. Key requirements:

- Must use an image-capable model - users frequently submit tickets consisting only of a screenshot with no descriptive text.
- In Phase 3, AI classification is the primary router for email-originated tickets.
- After Phase 4 is implemented, routing rules evaluate first and AI runs as a fallback for unstructured/no-match email tickets.
- Pre-routing AI can be evaluated in a future phase behind a feature flag.
- Cost should be minimized by using the smallest appropriate model and only calling the API when needed (e.g., email-sourced tickets without structured metadata).
- AI routing decisions should be logged for audit and for future model fine-tuning.

### Queues

Initial queues (configurable by admin):
- **Tech Support** - hardware, OS, Outlook, general IT issues
- **App Support** - Empower, Salesforce, Blitz, and other internal applications
- **General** - unmatched or ambiguous tickets, requires manual triage
- **New Hire / Termination** - access provisioning/de-provisioning workflow support

Future queues (as the team expands departmental coverage):
- Origination Support
- Processing Support
- Post-Closing Support
- Underwriting Support
- Funding Support

---

## Email Integration Flow

Header contract: `X-SupportHub-TicketId` is the canonical threading header across inbound/outbound processing.

### Inbound

Hangfire job polls each shared mailbox via Microsoft Graph API on a configurable interval (default: 1-2 minutes). Processing:

- New email with no matching thread: creates a new `Ticket` and `TicketMessage` (direction: Inbound).
- Reply to existing thread: matched via `X-SupportHub-TicketId` header (injected into all outbound replies) or subject line token. Appended as a new `TicketMessage` on the existing `Ticket`.
- AI classification step invoked if routing rules return no match.

### Outbound

When an agent replies, the system sends via Graph API from the company shared mailbox in v1.
Personal mailbox sender mode is post-v1 scope and requires delegated auth/governance.

The outbound message is recorded as a `TicketMessage` (direction: Outbound). The `X-SupportHub-TicketId` header is always injected to maintain thread matching on future replies.

---

## Audit & Compliance

SOX audit support was explicitly called out as a required feature. The system must be able to produce reports such as:

- All termination tickets in a given date range
- All new hire / access request tickets in a given date range
- Full ticket history for a given user or entity

Design decisions to support this:

- `AuditLog` entity captures all state changes (assignment, status change, tag addition, message send) with user and timestamp.
- `TicketTag` enables filtering by category (`termination`, `new-hire`, `access-request`, etc.) without requiring rigid schema enums.
- Soft-delete ensures all ticket data is retained indefinitely.
- SQL Server on-premises gives the team direct database access for ad-hoc reporting queries when needed.

---

## SLA Engine

- A Hangfire recurring job runs every few minutes, checking open tickets against their applicable SLA policy.
- Tracks two clocks: time to first response and time to resolution.
- When a threshold is approaching or breached, an `SlaBreachRecord` record is created and a notification surfaces in the UI (and optionally via email).
- v1 uses simple elapsed wall-clock time. Business hours support and customer-wait-time clock pausing are future enhancements - the data model is designed to accommodate these without breaking changes.

---

## Security Requirements

Explicitly raised by the team during kickoff as a non-negotiable requirement.

- All users authenticate via Azure AD (Microsoft Identity). No local accounts, no separate password management.
- Role-based authorization enforced at the API layer (never client-side only).
- No API keys, secrets, or credentials in frontend code. All sensitive configuration in server-side environment variables or Azure Key Vault.
- Multi-company data isolation enforced at the service/query layer - agents only see data for their assigned companies unless they hold the Super Admin role.
- File upload validation (MIME type, size limits, virus scanning consideration for v2).
- HTTPS enforced throughout; all internal API communication secured.

---

## Phased Build Order

### Phase 1 - Foundation 
- Solution structure: Blazor Web App project (Server interactivity), ASP.NET Core Web API project, shared class library, EF Core data project
- Azure AD authentication and role-based authorization (Super Admin, Admin, Agent)
- Company and entity management (data-driven, admin-configurable)
- Database schema and EF Core migrations
- Basic CI/CD pipeline in Azure DevOps
- Azure DevOps (Ralis) project board set up for sprint tracking *(action: John)*

### Phase 2 - Core Ticketing 
- Ticket CRUD (create via web form, assign, update status/priority, close)
- Structured web form with required field enforcement (entity, system, issue type, description)
- File and image attachment upload and on-premises storage
- Ticket list views with filtering (by company, status, agent, priority, queue, tag)
- Internal notes on tickets
- Canned responses (CRUD + insert into reply)
- Basic tagging (`new-hire`, `termination`, `access-request`, etc.)

### Phase 3 - Email Integration 
- Microsoft Graph API connection to shared mailboxes *(coordinate with IT for Mail.ReadWrite admin consent)*
- Inbound email polling and ticket creation/threading
- Outbound replies via company shared mailbox (v1)
- Email-to-ticket thread matching (`X-SupportHub-TicketId` header + subject fallback)
- AI classification for email-submitted tickets (Azure OpenAI, lightweight model)

### Phase 4 - Rules Engine & Routing UI 
- Admin UI for creating, editing, ordering, and enabling/disabling routing rules
- Rule evaluation pipeline (domain match, keyword match, form field match)
- Queue management UI
- Fallback-to-General logic for unmatched tickets

### Phase 5 - SLA & Satisfaction 
- SLA policy configuration per company/priority
- SLA monitoring background job
- SLA status indicators on ticket list and detail views
- Customer satisfaction survey (sent on ticket close, rating stored)

### Phase 6 - Audit Reporting & Knowledge Base 
- Audit/compliance reporting: ticket lists by tag and date range (terminations, new hires, access requests) - required for SOX
- Reporting dashboard: open/closed ticket counts, avg first response time, avg resolution time, SLA breach rate, tickets by company/agent/priority
- Internal KB article CRUD with search
- Agent KB article reference/linking in replies
- *Future: SharePoint RAG integration for self-service knowledge retrieval*

### Phase 7 - Polish & Hardening
- UI refinements, loading states, error handling, accessibility
- Full audit logging review and hardening
- Performance and load testing
- Documentation
- Production deployment and monitoring setup

---

## Project Team

| Name | Role |
|---|---|
| Pong Vang | Project Lead |
| Christopher Wilson | Developer / Architecture |
| Frank Chau | Developer |
| Brandon | Developer |
| David Moriguchi | Tech Support / Operations Lead (gatekeeper) |
| John Year | Infrastructure / DevOps |
| Randy Custodio | BSA - requirements capture and UAT |
| Quan Nguyen | CRM Manager |
| Jason Nguyen | End User / Requirements - App Support (TLE, CSBK, Empower, Salesforce) |
| Kimberly | End User / Requirements - App Support |
| Grace Gunti | QA |

---

## Risks & Considerations

- **WARNING Graph API permissions** - Mail.ReadWrite admin consent required for shared mailboxes. IT must be involved early. This is a hard dependency for Phase 3.
- **Email threading reliability** - A ticket ID token injected into the `X-SupportHub-TicketId` header is more reliable than subject line matching alone. Subject matching is the fallback only.
- **AI routing accuracy** - During Phase 3, AI is the primary router for email tickets; once Phase 4 is complete, rules become primary and AI fallback. Early monitoring of AI classification decisions is important to tune rules and catch misroutes before they become a pattern.
- **Image-only ticket submissions** - Users frequently submit tickets as screenshots with no text. The AI classification model must be image-capable to handle these. Rule-based routing will miss them entirely.
- **Ticket data quality** - The web form enforces required fields; email submissions do not. A proportion of email tickets will always require manual triage. Consider an auto-reply prompting email submitters to use the web form.
- **SLA clock pausing** - v1 uses simple elapsed time. The data model should anticipate a future "waiting on customer" clock-pause state so it can be added without a breaking migration.
- **Audit / SOX readiness** - The `AuditLog`, `TicketTag`, and soft-delete design satisfies the requirements discussed. Confirm specific report formats with Jason and the audit team before Phase 6.
- **Regulatory separation** - No specific compliance regulation applies to the support tier itself, but entity data must be clearly separated and labeled (`CompanyId` isolation) so auditors can view tickets scoped to a single entity.
- **On-premises hosting** - File storage and SQL Server on-premises keeps costs down and keeps data in the company data center. The `IFileStorageService` abstraction means Azure Blob is a swap-out if policy changes.
- **WARNING Timeline** - Baseline plan uses a 3-week Phase 1 (Weeks 1-3). The team flagged that requirements gathering and planning alone will take several days. A solid foundation with proper architecture will move faster in subsequent phases. Any 2-week target requires explicit scope cuts approved before sprint start.

---

## Immediate Next Steps

- **John Year:** Spin up Azure DevOps (Ralis) project board for Ralis Support Hub sprint tracking.
- **Christopher Wilson:** Finalize architecture document (GitHub repo) incorporating decisions from this meeting.
- **Frank / Chris / Brandon:** Review design and flag any technical blockers before sprint planning.
- **Jason / Kimberly:** Provide a representative sample of current ticket types, volumes, and common routing scenarios to inform rules engine configuration.
- **Pong:** Engage IT for Graph API admin consent (Mail.ReadWrite) on shared mailboxes. This is a hard dependency for Phase 3.
- **David / John:** Coordinate with Deepak on SQL Server instance provisioning for the project.
- **Randy:** Begin capturing formal requirements based on this kickoff. Coordinate with Jason and Kimberly on user stories for Phase 1 and 2.


