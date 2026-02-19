# Phase 3 — Email Integration

## Overview

Graph API shared mailbox polling, inbound email processing (create/append tickets), outbound email replies, `X-SupportHub-TicketId` header threading, AI classification interface stub.

---

## Prerequisites

- Phase 2 complete (Ticket, TicketMessage, TicketAttachment entities and services)
- Azure AD app registration with `Mail.ReadWrite`, `Mail.Send` delegated/application permissions
- Graph API admin consent granted
- Hangfire NuGet packages added

---

## Wave 1 — Domain Entities & Configuration

### EmailConfiguration Entity

| Property | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| CompanyId | Guid | FK → Company, required |
| SharedMailboxAddress | string | required, max 256 |
| DisplayName | string | required, max 200 |
| IsActive | bool | default `true` |
| PollingIntervalMinutes | int | default 2, range 1–60 |
| LastPolledAt | DateTimeOffset? | |
| LastPolledMessageId | string? | max 500 — Graph delta link or message ID for incremental polling |
| AutoCreateTickets | bool | default `true` |
| DefaultPriority | TicketPriority | default `Medium` |
| + BaseEntity fields | | CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt |

**Navigation:** `Company`

### EmailProcessingLog Entity (for debugging/audit)

| Property | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| EmailConfigurationId | Guid | FK → EmailConfiguration |
| ExternalMessageId | string | required, max 500 |
| Subject | string? | max 500 |
| SenderEmail | string? | max 256 |
| ProcessingResult | string | required — `Created`, `Appended`, `Skipped`, `Failed` |
| TicketId | Guid? | linked ticket if created/appended |
| ErrorMessage | string? | |
| ProcessedAt | DateTimeOffset | |

> **NOTE:** Does NOT inherit `BaseEntity` (immutable log).

### EF Configurations

**EmailConfigurationConfiguration:**
- Unique index on `CompanyId` + `SharedMailboxAddress`
- Index on `IsActive`

**EmailProcessingLogConfiguration:**
- Index on `EmailConfigurationId`
- Index on `ExternalMessageId`
- Index on `ProcessedAt`

### Migration

```bash
dotnet ef migrations add AddEmailIntegration --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
dotnet ef database update --project src/SupportHub.Infrastructure --startup-project src/SupportHub.Web
```

---

## Wave 2 — Graph API Client & Email Services

### IGraphClientFactory (Application)

```csharp
namespace SupportHub.Application.Interfaces;

public interface IGraphClientFactory
{
    GraphServiceClient CreateClient();
}
```

Implementation uses `Microsoft.Graph` SDK with app-only authentication (client credentials flow).

### IEmailPollingService (Application)

```csharp
namespace SupportHub.Application.Interfaces;

public interface IEmailPollingService
{
    Task<Result<int>> PollMailboxAsync(Guid emailConfigurationId, CancellationToken ct = default);
}
```

### IEmailSendingService (Application)

```csharp
namespace SupportHub.Application.Interfaces;

public interface IEmailSendingService
{
    Task<Result<bool>> SendReplyAsync(Guid ticketId, string body, string? htmlBody, IReadOnlyList<Guid>? attachmentIds, CancellationToken ct = default);
}
```

### IEmailProcessingService (Application)

```csharp
namespace SupportHub.Application.Interfaces;

public interface IEmailProcessingService
{
    Task<Result<Guid?>> ProcessInboundEmailAsync(InboundEmailMessage message, Guid emailConfigurationId, CancellationToken ct = default);
}
```

### InboundEmailMessage DTO

```csharp
namespace SupportHub.Application.DTOs;

public record InboundEmailMessage(
    string ExternalMessageId,
    string Subject,
    string Body,
    string? HtmlBody,
    string SenderEmail,
    string SenderName,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<EmailAttachment> Attachments,
    IReadOnlyDictionary<string, string> InternetHeaders
);

public record EmailAttachment(
    string FileName,
    string ContentType,
    long Size,
    Stream Content
);
```

### IAiClassificationService (Application — stub)

```csharp
namespace SupportHub.Application.Interfaces;

public interface IAiClassificationService
{
    Task<Result<AiClassificationResult>> ClassifyAsync(string subject, string body, Guid companyId, CancellationToken ct = default);
}

public record AiClassificationResult(
    string? SuggestedQueueName,
    IReadOnlyList<string> SuggestedTags,
    string? SuggestedIssueType,
    double Confidence,
    string ModelUsed,
    string RawResponse
);
```

---

## Wave 3 — Service Implementations

### EmailPollingService

1. Load `EmailConfiguration` by ID; verify `IsActive`.
2. Create Graph client via `IGraphClientFactory`.
3. Query messages from shared mailbox since `LastPolledAt` (use delta query or filter by `receivedDateTime`).
4. For each message:
   1. Check if already processed (`ExternalMessageId` in `EmailProcessingLog`) — skip if found.
   2. Extract `X-SupportHub-TicketId` header — if found, match to existing ticket.
   3. **Fallback:** parse subject for ticket number pattern (`TKT-XXXXXXXX-XXXX`).
   4. If match found — append as inbound `TicketMessage` via `ITicketMessageService`.
   5. If no match — create new ticket via `ITicketService` + `IEmailProcessingService`.
   6. Process attachments — save via `IAttachmentService`.
   7. Log result in `EmailProcessingLog`.
5. Update `LastPolledAt` and `LastPolledMessageId` on `EmailConfiguration`.
6. Return count of processed messages.

### EmailSendingService

1. Load ticket + company + email configuration.
2. Build Graph message with:
   - **From:** shared mailbox address
   - **To:** ticket requester email
   - **Subject:** `Re: [TKT-XXXXXXXX-XXXX] {original subject}`
   - **Custom header:** `X-SupportHub-TicketId` = `ticket.Id`
   - **Body:** reply content (plain text + optional HTML)
   - **Attachments:** if any IDs provided
3. Send via Graph API.
4. Create outbound `TicketMessage` record.
5. Log to audit via `AuditLog`.

### NoOpAiClassificationService (stub implementation)

Returns empty classification with `Confidence = 0`. Logs that AI classification was requested but no provider configured via `ILogger<NoOpAiClassificationService>`.

```csharp
namespace SupportHub.Infrastructure.Services;

public class NoOpAiClassificationService : IAiClassificationService
{
    private readonly ILogger<NoOpAiClassificationService> _logger;

    public NoOpAiClassificationService(ILogger<NoOpAiClassificationService> logger)
    {
        _logger = logger;
    }

    public Task<Result<AiClassificationResult>> ClassifyAsync(string subject, string body, Guid companyId, CancellationToken ct = default)
    {
        _logger.LogInformation("AI classification requested for CompanyId {CompanyId} but no provider is configured", companyId);

        var result = new AiClassificationResult(
            SuggestedQueueName: null,
            SuggestedTags: [],
            SuggestedIssueType: null,
            Confidence: 0,
            ModelUsed: "none",
            RawResponse: string.Empty
        );

        return Task.FromResult(Result<AiClassificationResult>.Success(result));
    }
}
```

### EmailProcessingService

1. Parse inbound email.
2. Attempt ticket matching (header check first, then subject fallback).
3. If new ticket: call `IAiClassificationService` for suggested routing/tags.
4. Create/append ticket and message via existing `ITicketService` / `ITicketMessageService`.
5. Store AI classification JSON on `Ticket.AiClassification` if applicable.
6. Return the ticket ID (new or existing) or `null` if skipped.

---

## Wave 4 — Hangfire Job Configuration

### EmailPollingJob

```csharp
namespace SupportHub.Infrastructure.Jobs;

public class EmailPollingJob
{
    private readonly IEmailPollingService _emailPollingService;
    private readonly IEmailConfigurationRepository _configRepository;
    private readonly ILogger<EmailPollingJob> _logger;

    public EmailPollingJob(
        IEmailPollingService emailPollingService,
        IEmailConfigurationRepository configRepository,
        ILogger<EmailPollingJob> logger)
    {
        _emailPollingService = emailPollingService;
        _configRepository = configRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Load all active EmailConfigurations
        // For each, check if polling interval has elapsed since LastPolledAt
        // Call IEmailPollingService.PollMailboxAsync
        // Log results via ILogger
    }
}
```

### Hangfire Setup in Program.cs

- Add Hangfire services with SQL Server storage (same database, `Hangfire` schema)
- Configure recurring job: `EmailPollingJob` every 1 minute
- Dashboard at `/hangfire` (restricted to `SuperAdmin` role)

```csharp
// In Program.cs — service registration
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "Hangfire"
    }));
builder.Services.AddHangfireServer();

// In Program.cs — middleware pipeline
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new SuperAdminDashboardAuthorizationFilter()]
});

// Recurring job registration
RecurringJob.AddOrUpdate<EmailPollingJob>(
    "email-polling",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Minutely);
```

---

## Wave 5 — Admin UI

### Email Configuration Page (`/admin/email-configurations`)

- `MudDataGrid` listing all email configurations grouped by company
- Add/Edit dialog with fields:
  - Company selector (`MudSelect`)
  - Shared mailbox address (`MudTextField`)
  - Display name (`MudTextField`)
  - Polling interval in minutes (`MudNumericField`, range 1–60)
  - Default priority (`MudSelect` bound to `TicketPriority`)
  - Auto-create tickets toggle (`MudSwitch`)
  - Active toggle (`MudSwitch`)
- **Test connection** button — attempts to list recent messages from the mailbox via Graph API
- **Last polled** timestamp display
- **Processing log viewer** — recent entries for the selected configuration

### Email Processing Log Viewer (`/admin/email-logs`)

- `MudDataGrid` with filtering:
  - Date range (`MudDateRangePicker`)
  - Company (`MudSelect`)
  - Result type: `Created` / `Appended` / `Skipped` / `Failed` (`MudChipSet` or `MudSelect`)
- Clickable links to associated tickets (navigates to ticket detail)
- Export to CSV option

---

## Wave 6 — API Endpoints

### EmailConfigurationsController

| Method | Route | Description | Auth |
|---|---|---|---|
| GET | `/api/email-configurations` | List all configurations | Admin+ |
| GET | `/api/email-configurations/{id}` | Single configuration | Admin+ |
| POST | `/api/email-configurations` | Create new configuration | Admin+ |
| PUT | `/api/email-configurations/{id}` | Update configuration | Admin+ |
| POST | `/api/email-configurations/{id}/test` | Test mailbox connection | Admin+ |
| GET | `/api/email-configurations/{id}/logs` | Processing logs for config | Admin+ |

### Webhook Endpoint (future-proofing)

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/email/inbound` | Receive email push notifications | API Key |

> Reserved for future push-based integration (e.g., Graph subscriptions/webhooks). Not implemented in Phase 3 but the route is registered to prevent conflicts.

---

## Wave 7 — Tests

### Unit Tests

**EmailPollingServiceTests:**
- Poll new messages and process them successfully
- Skip already-processed messages (duplicate `ExternalMessageId`)
- Handle empty mailbox gracefully (zero messages)
- Handle inactive configuration (returns error)
- Update `LastPolledAt` after successful poll

**EmailProcessingServiceTests:**
- Create new ticket from email (no matching header or subject)
- Append to existing ticket via `X-SupportHub-TicketId` header match
- Append to existing ticket via subject line fallback (`TKT-XXXXXXXX-XXXX`)
- Handle email attachments (saves via `IAttachmentService`)
- Store AI classification result when creating new ticket
- Skip processing when `AutoCreateTickets` is `false` and no existing ticket match

**EmailSendingServiceTests:**
- Send reply with correct `X-SupportHub-TicketId` header
- Include attachments when attachment IDs provided
- Create outbound `TicketMessage` record after sending
- Format subject as `Re: [TKT-XXXXXXXX-XXXX] {original subject}`
- Return error when ticket not found

**NoOpAiClassificationServiceTests:**
- Returns result with `Confidence = 0`
- Returns empty suggested tags and null queue/issue type
- Logs informational message

**EmailPollingJobTests:**
- Processes all active configurations
- Skips inactive configurations
- Respects polling interval (skips configs not yet due)
- Handles errors gracefully (one config failure does not block others)
- Logs summary of polling results

### Mocking Strategy

Mock Graph API calls using `NSubstitute` on `IGraphClientFactory`. Mock all service dependencies (`ITicketService`, `ITicketMessageService`, `IAttachmentService`, `IAiClassificationService`) to isolate each service under test.

---

## Acceptance Criteria

- [ ] `EmailConfiguration` and `EmailProcessingLog` entities created with proper EF config
- [ ] Migration runs successfully (`AddEmailIntegration`)
- [ ] Email polling reads messages from Graph API shared mailbox
- [ ] New emails create tickets with correct requester info
- [ ] Reply emails append to existing tickets (`X-SupportHub-TicketId` header match)
- [ ] Subject line fallback matching works for ticket threading
- [ ] Outbound replies sent via shared mailbox with correct headers
- [ ] Attachments from emails saved and linked to tickets/messages
- [ ] Hangfire job runs on schedule and polls active configurations
- [ ] Email configuration admin page works end-to-end
- [ ] Processing logs capture all email processing outcomes
- [ ] AI classification interface exists (stub implementation returns empty result)
- [ ] Already-processed emails are skipped (no duplicates)
- [ ] All unit tests pass
- [ ] `dotnet build` — zero errors, zero warnings

---

## Dependencies

| Dependency | Source |
|---|---|
| `Ticket`, `TicketMessage`, `TicketAttachment` entities | Phase 2 |
| `ITicketService`, `ITicketMessageService`, `IAttachmentService` | Phase 2 |
| Azure AD Graph API permissions (`Mail.ReadWrite`, `Mail.Send`) | External — Azure AD admin |
| Shared mailbox access | External — Exchange/M365 admin |
| `Microsoft.Graph` NuGet package | NuGet |
| `Hangfire`, `Hangfire.SqlServer` NuGet packages | NuGet |

---

## Next Phase

Phase 4 (Routing) builds the rules engine and queue management that integrates with both web form and email ticket creation from this phase.
