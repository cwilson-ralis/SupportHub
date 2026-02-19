# Phase 5 - SLA & Customer Satisfaction

> **Prerequisites:** Phases 0-4 complete. Tickets are being created via portal and email. Routing engine is assigning tickets to divisions. `FirstResponseAt` and `ResolvedAt` are being tracked on tickets. Hangfire is running for background jobs.

---

## Objective

Implement SLA policy management, automated SLA monitoring with breach detection and notifications, and customer satisfaction surveys sent on ticket closure. At the end of this phase, admins can configure SLA targets per company/priority, agents see real-time SLA countdown on tickets, and breaches are tracked and surfaced.

---

## Task 5.1 - SLA Policy Management

### Instructions

1. **Create `ISlaPolicyService`** in `Core/Interfaces/`:

```csharp
public interface ISlaPolicyService
{
 Task<Result<List<SlaPolicyDto>>> GetByCompanyIdAsync(int companyId);
 Task<Result<SlaPolicyDto>> CreateOrUpdateAsync(CreateSlaPolicyDto dto);
 Task<Result<bool>> DeleteAsync(int id);
 Task<Result<SlaPolicyDto?>> GetApplicablePolicyAsync(int companyId, TicketPriority priority);
}
```

2. **Create DTOs:**
 - `SlaPolicyDto`: Id, CompanyId, CompanyName, Priority (enum + display name), FirstResponseMinutes, ResolutionMinutes, FirstResponseFormatted (e.g., "4 hours"), ResolutionFormatted (e.g., "24 hours")
 - `CreateSlaPolicyDto`: CompanyId, Priority, FirstResponseMinutes, ResolutionMinutes

3. **Implement `SlaPolicyService`:**
 - Enforce unique constraint: one policy per (CompanyId, Priority)
 - `CreateOrUpdateAsync` performs an upsert - if a policy exists for the company + priority, update it; otherwise create
 - Validate: FirstResponseMinutes > 0, ResolutionMinutes >= FirstResponseMinutes
 - Only Admin/SuperAdmin can manage

4. **Create Blazor page `Pages/Admin/SlaPolicies.razor`:**
 - Company selector at top
 - Show a table/grid with one row per priority level (Low, Medium, High, Urgent)
 - Each row shows: Priority, First Response Target (editable), Resolution Target (editable)
 - Inline editing with save button per row (or save all)
 - Use `MudTimePicker` or numeric inputs with hours/minutes formatting
 - Show "Not Configured" for priorities without a policy
 - Provide a "Copy from another company" action for convenience

---

## Task 5.2 - SLA Calculation Service

### Instructions

1. **Create `ISlaCalculationService`** in `Core/Interfaces/`:

```csharp
public interface ISlaCalculationService
{
 Task<SlaStatusDto> CalculateStatusAsync(int ticketId);
 Task<List<SlaStatusDto>> CalculateStatusBatchAsync(List<int> ticketIds);
}
```

2. **Create `SlaStatusDto`:**

```csharp
public record SlaStatusDto
{
 public int TicketId { get; init; }
 public bool HasPolicy { get; init; }

 // First Response SLA
 public int? FirstResponseTargetMinutes { get; init; }
 public int? FirstResponseElapsedMinutes { get; init; }
 public int? FirstResponseRemainingMinutes { get; init; }
 public bool FirstResponseMet { get; init; } // responded within target
 public bool FirstResponseBreached { get; init; } // target exceeded, no response yet
 public SlaUrgency FirstResponseUrgency { get; init; }

 // Resolution SLA
 public int? ResolutionTargetMinutes { get; init; }
 public int? ResolutionElapsedMinutes { get; init; }
 public int? ResolutionRemainingMinutes { get; init; }
 public bool ResolutionMet { get; init; }
 public bool ResolutionBreached { get; init; }
 public SlaUrgency ResolutionUrgency { get; init; }
}

public enum SlaUrgency
{
 None, // No SLA configured or already met
 OnTrack, // > 50% time remaining
 Warning, // 25%-50% time remaining
 Critical, // < 25% time remaining
 Breached // Time exceeded
}
```

3. **Implement `SlaCalculationService`:**

 **Calculation logic:**

 ```
 For a given ticket:
 1. Look up the SlaPolicy for (ticket.CompanyId, ticket.Priority)
 - If no policy exists -> return HasPolicy = false, all fields null/None
 2. First Response SLA:
 - If ticket.FirstResponseAt is set:
 - ElapsedMinutes = (FirstResponseAt - CreatedAt).TotalMinutes
 - FirstResponseMet = ElapsedMinutes <= TargetMinutes
 - RemainingMinutes = 0
 - Urgency = None (already responded)
 - Else (no response yet):
 - ElapsedMinutes = (UtcNow - CreatedAt).TotalMinutes
 - RemainingMinutes = TargetMinutes - ElapsedMinutes
 - FirstResponseBreached = ElapsedMinutes > TargetMinutes
 - Urgency = calculate from percentage remaining
 3. Resolution SLA:
 - If ticket.ResolvedAt is set:
 - ElapsedMinutes = (ResolvedAt - CreatedAt).TotalMinutes
 - ResolutionMet = ElapsedMinutes <= TargetMinutes
 - Urgency = None
 - Else if ticket is Closed and ClosedAt is set:
 - Same as resolved but use ClosedAt
 - Else (still open):
 - ElapsedMinutes = (UtcNow - CreatedAt).TotalMinutes
 - RemainingMinutes = TargetMinutes - ElapsedMinutes
 - ResolutionBreached = ElapsedMinutes > TargetMinutes
 - Urgency = calculate from percentage remaining
 ```

 **Urgency calculation:**
 ```
 percentRemaining = RemainingMinutes / TargetMinutes * 100
 if percentRemaining > 50 -> OnTrack
 if percentRemaining > 25 -> Warning
 if percentRemaining > 0 -> Critical
 if percentRemaining <= 0 -> Breached
 ```

 **Batch calculation** should be efficient - load all tickets and policies in a single query, calculate in memory.

---

## Task 5.3 - SLA Monitoring Background Job

### Instructions

1. **Create `SlaMonitoringJob`** in `Infrastructure/Services/`:

 ```
 Runs on a Hangfire recurring schedule (every 5 minutes):

 1. Query all open tickets (Status not in [Resolved, Closed]) that have an SLA policy
 2. For each ticket, calculate SLA status
 3. For tickets that are newly breached (no existing SlaBreachRecord for this breach type):
 a. Create a SlaBreachRecord in the database
 b. Send a notification (see Task 5.4)
 4. Log summary: X tickets checked, Y new breaches detected
 ```

2. **Register the Hangfire job:**

```csharp
recurringJobManager.AddOrUpdate<SlaMonitoringJob>(
 "sla-monitoring",
 job => job.ExecuteAsync(),
 "*/5 * * * *"); // Every 5 minutes
```

3. **Important:** The job must be **idempotent** - running it multiple times should not create duplicate breach records. Check for existing `SlaBreachRecord` by (TicketId, BreachType) before creating.

---

## Task 5.4 - SLA Breach Notifications

### Instructions

1. **Create `ISlaNotificationService`** in `Core/Interfaces/`:

```csharp
public interface ISlaNotificationService
{
 Task NotifyBreachAsync(int ticketId, SlaBreachType breachType);
 Task NotifyWarningAsync(int ticketId, SlaBreachType concernType, int minutesRemaining);
}
```

2. **Implement `SlaNotificationService`:**

 **On breach:**
 - Send an email to the assigned agent (if any) via Graph API
 - If the ticket is unassigned, send to all agents assigned to the company
 - Email subject: `[SLA BREACH] Ticket SH-{ticketId}: {breachType} target exceeded`
 - Email body: ticket subject, requester, company, how far past the SLA, link to ticket

 **On warning (Critical urgency, < 25% time remaining):**
 - Same recipients as breach
 - Subject: `[SLA WARNING] Ticket SH-{ticketId}: {breachType} target approaching`
 - Only send once per ticket per warning level (track in a `SlaNotificationLog` table or a simple flag)

3. **Create `SlaNotificationLog` entity** to prevent duplicate notifications:
 - `int TicketId`
 - `SlaBreachType BreachType`
 - `string NotificationType` ("Warning" or "Breach")
 - `DateTimeOffset SentAt`
 - Unique constraint on (TicketId, BreachType, NotificationType)

---

## Task 5.5 - SLA Display in Ticket UI

### Instructions

1. **Update the Ticket Detail page** (Phase 2, Task 2.7) right-side properties panel:

 Replace the "SLA not configured" placeholder with a real SLA widget:

 ```
 +-----------------------------+
 | SLA Status                  |
 +-----------------------------+
 | First Response              |
 | [Met] responded in 45min    | <- Green checkmark if met
 | Target: 1 hour              |
 +-----------------------------+
 | Resolution                  |
 | 3h 15m remaining            | <- Countdown if still open
 | Target: 8 hours             |
 | [########---] 60%           | <- Progress bar, color by urgency
 +-----------------------------+
 ```

 - Use `MudProgressLinear` for the progress bar
 - Colors: OnTrack = green, Warning = amber, Critical = orange, Breached = red
 - Show "No SLA Policy" if the ticket's company + priority has no policy
 - If breached, show how far past the target: "Breached by 2h 30m"

2. **Update the Ticket List page** (Phase 2, Task 2.6):

 - Add an "SLA" column showing a small indicator:
 - Green dot: on track
 - Amber dot: warning
 - Red dot: critical or breached
 - Gray dot: no SLA or already met
 - Use `ISlaCalculationService.CalculateStatusBatchAsync` to efficiently compute for the visible page
 - Add an SLA filter: "Show only breached", "Show only at risk (Warning + Critical)"

---

## Task 5.6 - Customer Satisfaction Surveys

### Instructions

1. **Create `ISatisfactionService`** in `Core/Interfaces/`:

```csharp
public interface ISatisfactionService
{
 Task<Result<bool>> SendSurveyAsync(int ticketId);
 Task<Result<CustomerSatisfactionRatingDto>> SubmitRatingAsync(int ticketId, SubmitRatingDto dto);
 Task<Result<CustomerSatisfactionRatingDto?>> GetByTicketIdAsync(int ticketId);
}
```

2. **Create DTOs:**
 - `CustomerSatisfactionRatingDto`: TicketId, Score, Comment, SubmittedAt
 - `SubmitRatingDto`: Score (1-5, required), Comment (optional, max 2000)

3. **Implement survey flow:**

 **Triggering the survey:**
 - When a ticket status changes to `Closed`, automatically call `SendSurveyAsync`
 - `SendSurveyAsync` sends an email to `Ticket.RequesterEmail` via Graph API:
 - Subject: `How did we do? Ticket SH-{ticketId}`
 - Body: Simple HTML email with:
 - "Your ticket '{subject}' has been closed."
 - 5 clickable star/number links (1-5) that link to: `{appBaseUrl}/rate/{ticketId}?score={1-5}&token={token}`
 - A note that they can add a comment on the rating page

 **Rating token:**
 - Generate a short-lived token (HMAC-SHA256 of ticketId + secret + expiry)
 - Token is valid for 7 days
 - This allows the customer to rate without logging in (they are external-facing in the sense that they receive email, even though they are internal AD users - but the email link should work without requiring auth)
 - Validate token server-side before loading any ticket metadata
 - Invalid/expired tokens must return a generic expired/invalid message without exposing ticket details

 **Public endpoint safeguards (required in Phase 5):**
 - Add rate limiting on `/rate/*` endpoints to prevent abuse
 - Enforce single-submission behavior for each ticket rating (idempotent duplicate protection)
 - Reject expired/invalid tokens before rendering ticket-specific content

4. **Create a public (no auth) Blazor page** `Pages/Public/RateTicket.razor`:
 - Route: `/rate/{ticketId:int}`
 - Query params: `score` (pre-selected), `token`
 - Validate the token; if invalid/expired, show "This rating link has expired"
 - Show: ticket subject (read-only), 1-5 star rating (pre-selected from URL), optional comment text area, Submit button
 - On submit, save the `CustomerSatisfactionRating`
 - If a rating already exists for this ticket, show "You've already rated this ticket" with their previous rating
 - Simple, clean, no-nav-bar page - just the rating form

5. **Show the rating on the Ticket Detail page:**
 - In the properties panel, below SLA status:
 - If rated: show the score as stars and the comment
 - If not rated and ticket is closed: show "Awaiting customer feedback"
 - If not yet closed: show nothing

---

## Task 5.7 - API Endpoints

### Instructions

1. **SLA Policies:**

```
GET /api/v1/companies/{companyId}/sla-policies -> GetByCompany
POST /api/v1/companies/{companyId}/sla-policies -> CreateOrUpdate
DELETE /api/v1/sla-policies/{id} -> Delete
```

2. **SLA Status:**

```
GET /api/v1/tickets/{ticketId}/sla-status -> GetSlaStatus
```

3. **Customer Satisfaction:**

```
POST /api/v1/tickets/{ticketId}/satisfaction -> SubmitRating (public, token-validated)
GET /api/v1/tickets/{ticketId}/satisfaction -> GetRating (authenticated)
```

---

## Task 5.8 - Testing

### Instructions

1. **Unit tests for `SlaCalculationService`:**
 - Test: ticket with no SLA policy -> HasPolicy = false
 - Test: ticket with first response within target -> FirstResponseMet = true
 - Test: ticket with first response over target -> FirstResponseMet = false
 - Test: open ticket approaching SLA -> correct urgency levels at 60%, 40%, 20%, 0% remaining
 - Test: resolved ticket -> resolution SLA calculated from CreatedAt to ResolvedAt
 - Test: batch calculation efficiency (all loaded in one query)

2. **Unit tests for `SlaMonitoringJob`:**
 - Test: detects new breach and creates SlaBreachRecord
 - Test: does not create duplicate breach records on re-run (idempotent)
 - Test: sends notification on breach

3. **Unit tests for `SatisfactionService`:**
 - Test: token generation and validation
 - Test: expired token is rejected
 - Test: duplicate rating is rejected
 - Test: valid rating is saved

---

## Acceptance Criteria for Phase 5

- [ ] Admins can configure SLA policies per company per priority level
- [ ] Ticket detail page shows real-time SLA countdown with progress bar and urgency colors
- [ ] Ticket list page shows SLA indicator dots and supports filtering by SLA status
- [ ] SLA monitoring job detects breaches and creates breach records
- [ ] Breach and warning email notifications are sent to assigned agents
- [ ] Notifications are not duplicated on repeated job runs
- [ ] When a ticket is closed, a satisfaction survey email is sent
- [ ] Customers can rate tickets 1-5 via a token-validated public page
- [ ] Public rating endpoints are rate-limited and do not expose ticket details for invalid/expired tokens
- [ ] Duplicate rating submissions are prevented
- [ ] Ratings are displayed on the ticket detail page
- [ ] All new services have unit tests
- [ ] SLA calculations are correct across all ticket states


