# Ralis Support Hub — Build Progress

## How to Use This File
The orchestrator updates this file after each wave. Sub-agents read it to understand what's been completed. Each task is marked with the agent that completed it.

---

## Phase 1 — Foundation
### Wave 1 — Domain Core & Shared Abstractions
- [ ] BaseEntity class — backend
- [ ] Result&lt;T&gt; and PagedResult&lt;T&gt; — backend
- [ ] UserRole enum — backend

### Wave 2 — Entities
- [ ] Company entity — backend
- [ ] Division entity — backend
- [ ] ApplicationUser entity — backend
- [ ] UserCompanyRole entity — backend
- [ ] AuditLogEntry entity — backend

### Wave 3 — EF Core Configuration & DbContext
- [ ] SupportHubDbContext with soft-delete filter — backend
- [ ] AuditableEntityInterceptor — backend
- [ ] Entity configurations (Company, Division, ApplicationUser, UserCompanyRole, AuditLogEntry) — backend
- [ ] Initial migration — backend

### Wave 4 — Authentication & Authorization
- [ ] Azure AD configuration — service
- [ ] ICurrentUserService interface + implementation — backend/service
- [ ] Authorization policies (SuperAdmin, Admin, Agent) — service

### Wave 5 — Service Interfaces & Implementations
- [ ] ICompanyService + CompanyService — backend/service
- [ ] IUserService + UserService — backend/service
- [ ] IAuditService + AuditService — backend/service
- [ ] DTOs (CompanyDto, UserDto, etc.) — backend

### Wave 6 — Blazor UI Pages
- [ ] MainLayout + NavMenu — ui
- [ ] Company List page — ui
- [ ] Company Detail/Edit page — ui
- [ ] User List page — ui
- [ ] User Detail page — ui
- [ ] Dashboard placeholder — ui
- [ ] CompaniesController — api
- [ ] UsersController — api

### Wave 7 — Tests
- [ ] CompanyServiceTests — test
- [ ] UserServiceTests — test
- [ ] AuditServiceTests — test

**Phase 1 Status:** Not Started

---

## Phase 2 — Core Ticketing
### Wave 1 — Domain Entities & Enums
- [ ] TicketStatus, TicketPriority, TicketSource, MessageDirection enums — backend
- [ ] Ticket entity — backend
- [ ] TicketMessage entity — backend
- [ ] TicketAttachment entity — backend
- [ ] InternalNote entity — backend
- [ ] TicketTag entity — backend
- [ ] CannedResponse entity — backend

### Wave 2 — EF Configurations & Migration
- [ ] Entity configurations for all Phase 2 entities — backend
- [ ] AddCoreTicketing migration — backend

### Wave 3 — Service Interfaces & DTOs
- [ ] All Phase 2 DTOs — backend
- [ ] ITicketService, ITicketMessageService, IInternalNoteService — backend
- [ ] IFileStorageService, IAttachmentService — backend
- [ ] ICannedResponseService, ITagService — backend

### Wave 4 — Service Implementations
- [ ] TicketService — service
- [ ] TicketMessageService — service
- [ ] InternalNoteService — service
- [ ] LocalFileStorageService — infrastructure
- [ ] AttachmentService — service
- [ ] CannedResponseService — service
- [ ] TagService — service

### Wave 5 — Blazor UI Pages & Components
- [ ] Create Ticket page — ui
- [ ] Ticket List page — ui
- [ ] Ticket Detail page — ui
- [ ] Canned Responses Admin page — ui
- [ ] Shared components (StatusChip, PriorityChip, Timeline, etc.) — ui

### Wave 6 — API Controllers
- [ ] TicketsController — api
- [ ] TicketMessagesController — api
- [ ] TicketNotesController — api
- [ ] TicketAttachmentsController — api
- [ ] TicketTagsController — api
- [ ] CannedResponsesController — api

### Wave 7 — Tests
- [ ] TicketServiceTests — test
- [ ] TicketMessageServiceTests — test
- [ ] InternalNoteServiceTests — test
- [ ] AttachmentServiceTests — test
- [ ] CannedResponseServiceTests — test
- [ ] TagServiceTests — test

**Phase 2 Status:** Not Started

---

## Phase 3 — Email Integration
### Wave 1 — Domain Entities & Configuration
- [ ] EmailConfiguration entity — backend
- [ ] EmailProcessingLog entity — backend
- [ ] EF configurations + migration — backend

### Wave 2 — Graph API Client & Email Services
- [ ] IGraphClientFactory interface + implementation — backend/infrastructure
- [ ] IEmailPollingService, IEmailSendingService, IEmailProcessingService interfaces — backend
- [ ] IAiClassificationService interface — backend
- [ ] InboundEmailMessage + related DTOs — backend

### Wave 3 — Service Implementations
- [ ] EmailPollingService — infrastructure
- [ ] EmailSendingService — infrastructure
- [ ] EmailProcessingService — infrastructure
- [ ] NoOpAiClassificationService — infrastructure

### Wave 4 — Hangfire Job Configuration
- [ ] EmailPollingJob — infrastructure
- [ ] Hangfire setup in Program.cs — infrastructure

### Wave 5 — Admin UI
- [ ] Email Configuration page — ui
- [ ] Email Processing Log Viewer — ui

### Wave 6 — API Endpoints
- [ ] EmailConfigurationsController — api

### Wave 7 — Tests
- [ ] EmailPollingServiceTests — test
- [ ] EmailProcessingServiceTests — test
- [ ] EmailSendingServiceTests — test

**Phase 3 Status:** Not Started

---

## Phase 4 — Routing & Queue Management
### Wave 1 — Domain Entities & Enums
- [ ] RuleMatchType, RuleMatchOperator enums — backend
- [ ] Queue entity — backend
- [ ] RoutingRule entity — backend

### Wave 2 — EF Configurations & Migration
- [ ] QueueConfiguration, RoutingRuleConfiguration — backend
- [ ] AddRoutingAndQueues migration — backend

### Wave 3 — Service Interfaces & DTOs
- [ ] Queue and RoutingRule DTOs — backend
- [ ] IQueueService, IRoutingRuleService, IRoutingEngine interfaces — backend

### Wave 4 — Service Implementations
- [ ] QueueService — service
- [ ] RoutingRuleService — service
- [ ] RoutingEngine — service
- [ ] Integration into TicketService + EmailProcessingService — service

### Wave 5 — Blazor UI
- [ ] Queue Management page — ui
- [ ] Routing Rules page (with drag-drop) — ui
- [ ] Ticket list/detail queue display — ui

### Wave 6 — API Controllers
- [ ] QueuesController — api
- [ ] RoutingRulesController — api

### Wave 7 — Tests
- [ ] QueueServiceTests — test
- [ ] RoutingRuleServiceTests — test
- [ ] RoutingEngineTests — test

**Phase 4 Status:** Not Started

---

## Phase 5 — SLA & Customer Satisfaction
### Wave 1 — Domain Entities
- [ ] SlaPolicy entity — backend
- [ ] SlaBreachRecord entity — backend
- [ ] SlaBreachType enum — backend
- [ ] CustomerSatisfactionRating entity — backend

### Wave 2 — EF Configurations & Migration
- [ ] Entity configurations — backend
- [ ] AddSlaAndSatisfaction migration — backend

### Wave 3 — Service Interfaces & DTOs
- [ ] SLA and CSAT DTOs — backend
- [ ] ISlaPolicyService, ISlaMonitoringService, ICustomerSatisfactionService — backend

### Wave 4 — Service Implementations
- [ ] SlaPolicyService — service
- [ ] SlaMonitoringService — service
- [ ] CustomerSatisfactionService — service
- [ ] SlaMonitoringJob — infrastructure

### Wave 5 — Blazor UI
- [ ] SLA Policy Admin page — ui
- [ ] SLA Breaches page — ui
- [ ] Ticket list/detail SLA indicators — ui
- [ ] CSAT survey component — ui

### Wave 6 — API Controllers
- [ ] SlaPoliciesController — api
- [ ] SlaBreachesController — api
- [ ] CustomerSatisfactionController — api

### Wave 7 — Tests
- [ ] SlaPolicyServiceTests — test
- [ ] SlaMonitoringServiceTests — test
- [ ] CustomerSatisfactionServiceTests — test

**Phase 5 Status:** Not Started

---

## Phase 6 — Knowledge Base & Reporting
### Wave 1 — Domain Entities
- [ ] KnowledgeBaseArticle entity — backend

### Wave 2 — EF Configuration & Migration
- [ ] KnowledgeBaseArticleConfiguration — backend
- [ ] AddKnowledgeBase migration — backend

### Wave 3 — Service Interfaces & DTOs
- [ ] KB, Dashboard, Report DTOs — backend
- [ ] IKnowledgeBaseService, IDashboardService, IReportService — backend

### Wave 4 — Service Implementations
- [ ] KnowledgeBaseService — service
- [ ] DashboardService — service
- [ ] ReportService (with CSV export) — service

### Wave 5 — Blazor UI
- [ ] Dashboard page (replace placeholder) — ui
- [ ] Audit Report page — ui
- [ ] Ticket Report page — ui
- [ ] KB List/Search page — ui
- [ ] KB Article View page — ui
- [ ] KB Admin page — ui

### Wave 6 — API Controllers
- [ ] DashboardController — api
- [ ] ReportsController — api
- [ ] KnowledgeBaseController — api

### Wave 7 — Tests
- [ ] KnowledgeBaseServiceTests — test
- [ ] DashboardServiceTests — test
- [ ] ReportServiceTests — test

**Phase 6 Status:** Not Started

---

## Phase 7 — Hardening & Production Readiness
### Wave 1 — Audit & Security Validation
- [ ] Audit log completeness review — reviewer
- [ ] Company isolation audit — reviewer
- [ ] Authorization review — reviewer
- [ ] Input validation review — reviewer

### Wave 2 — Performance & Database Optimization
- [ ] Performance indexes migration — backend
- [ ] Query optimization (N+1, AsNoTracking, projection) — service
- [ ] Caching implementation — service
- [ ] Connection resiliency — infrastructure

### Wave 3 — Observability & Health Checks
- [ ] Serilog configuration — infrastructure
- [ ] Health checks (DB, Graph API, file storage) — infrastructure
- [ ] Request logging middleware — api

### Wave 4 — SignalR Real-Time Updates
- [ ] TicketHub — infrastructure
- [ ] INotificationService + implementation — backend/infrastructure
- [ ] UI integration (auto-refresh, toasts) — ui

### Wave 5 — API Documentation & Accessibility
- [ ] Swagger/OpenAPI configuration — api
- [ ] Accessibility review — ui
- [ ] Error handling (global exception handler, error boundary) — api/ui

### Wave 6 — Integration Tests
- [ ] Test infrastructure (WebApplicationFactory, helpers) — test
- [ ] Auth integration tests — test
- [ ] Ticket lifecycle integration tests — test
- [ ] Email processing integration tests — test
- [ ] Routing integration tests — test

### Wave 7 — Final Review
- [ ] Code quality (dotnet format, static analysis) — reviewer
- [ ] Configuration review (no hardcoded secrets) — reviewer
- [ ] Deployment preparation — infrastructure

**Phase 7 Status:** Not Started
