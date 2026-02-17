# Phase 3 — Email Integration (Weeks 7–8)

> **Prerequisites:** Phase 0 (conventions), Phase 1 (foundation), and Phase 2 (core ticketing) must be complete. Companies with shared mailbox addresses are configured. Ticket creation and messaging are working.

---

## Objective

Connect SupportHub to Microsoft 365 shared mailboxes via Microsoft Graph API. Inbound emails auto-create tickets or append to existing conversations. Agent replies are sent as email from either the shared mailbox or the agent's personal address. At the end of this phase, a customer can email `support@companyA.com` and receive a reply from an agent without ever visiting the portal.

---

## Task 3.1 — Microsoft Graph API Setup

### Instructions

1. **Azure AD App Permissions** — Document in `docs/GraphApiSetup.md`:
   - The existing App Registration from Phase 1 needs additional **application-level** permissions (not delegated), because email polling runs as a background job with no user context:
     - `Mail.ReadWrite` (application) — read and manage mail in shared mailboxes
     - `Mail.Send` (application) — send mail as shared mailbox or user
   - These require **admin consent** from a tenant administrator
   - Record the tenant ID, client ID, and client secret in configuration (use User Secrets for local dev, Key Vault for production)

2. **Create `IGraphClientFactory`** in `Core/Interfaces/`:

```csharp
public interface IGraphClientFactory
{
    GraphServiceClient CreateClient();
}
```

3. **Implement `GraphClientFactory`** in `Infrastructure/Email/`:
   - Use `ClientSecretCredential` (from `Azure.Identity`) with tenant ID, client ID, client secret
   - Create and return a `GraphServiceClient` configured with the credential
   - Register as a **singleton** in DI (the underlying `HttpClient` should be reused)

4. **Create `EmailSettings`** configuration class in `Core/`:

```csharp
public class EmailSettings
{
    public int PollingIntervalSeconds { get; set; } = 60;
    public string TicketIdHeaderName { get; set; } = "X-SupportHub-TicketId";
    public int MaxEmailBodyLengthChars { get; set; } = 50000;
    public string[] IgnoredSenderPatterns { get; set; } = ["noreply@", "mailer-daemon@"];
}
```

---

## Task 3.2 — Inbound Email Processing Service

### Instructions

1. **Create `IEmailIngestionService`** in `Core/Interfaces/`:

```csharp
public interface IEmailIngestionService
{
    Task ProcessInboxAsync(int companyId, CancellationToken cancellationToken);
}
```

2. **Implement `EmailIngestionService`** in `Infrastructure/Email/`:

   **High-level flow for `ProcessInboxAsync(companyId)`:**

   ```
   1. Look up the Company to get its SharedMailboxAddress
   2. Call Graph API: GET /users/{sharedMailboxAddress}/mailFolders/inbox/messages
      - Filter: isRead eq false
      - Select: id, subject, body, from, toRecipients, receivedDateTime, internetMessageHeaders, hasAttachments
      - Order by: receivedDateTime asc
      - Top: 50 (process in batches)
   3. For each unread email:
      a. Check if sender matches any IgnoredSenderPatterns → skip
      b. Check for duplicate by ExternalMessageId (Graph message ID) → skip if exists
      c. Try to match to an existing ticket (see matching logic below)
      d. If matched → append as a new TicketMessage to the existing ticket
      e. If not matched → create a new Ticket with the email as the first message
      f. Process attachments (see Task 3.3)
      g. Mark the email as read in Graph API: PATCH /users/{mailbox}/messages/{id} → isRead = true
      h. Move the email to a "Processed" folder (create if it doesn't exist)
   4. Log all actions (created ticket #X, appended to ticket #Y, skipped email Z)
   ```

   **Ticket matching logic (in priority order):**

   1. **Custom header match:** Check `internetMessageHeaders` for `X-SupportHub-TicketId`. If present and the ticket ID exists and belongs to this company, match it.
   2. **Subject line match:** Look for a pattern like `[SH-{ticketId}]` in the subject. Parse the ID, verify it exists and belongs to this company.
   3. **Thread match by In-Reply-To header:** Check the `In-Reply-To` or `References` headers against stored `ExternalMessageId` values on existing `TicketMessage` records for this company.
   4. **No match:** Create a new ticket.

   **Creating a ticket from email:**
   - `Subject` = email subject (strip `RE:`, `FW:` prefixes, trim)
   - `RequesterEmail` = from.emailAddress.address
   - `RequesterName` = from.emailAddress.name (fallback to email address if name is empty)
   - `CompanyId` = the company being processed
   - `Source` = `TicketSource.Email`
   - `Status` = `TicketStatus.New`
   - `Priority` = `TicketPriority.Medium` (default; can be enhanced with rules later)
   - First `TicketMessage`:
     - `Body` = email body content (prefer text content, fallback to HTML with sanitization)
     - `Direction` = `Inbound`
     - `ExternalMessageId` = Graph message ID

   **Appending to an existing ticket:**
   - Create a new `TicketMessage` on the matched ticket
   - If ticket status is `AwaitingCustomer`, change it to `AwaitingAgent` (customer replied)
   - If ticket status is `Closed` or `Resolved`, change it to `Open` (reopen on customer reply)

3. **HTML Sanitization:**
   - Install `HtmlSanitizer` NuGet package
   - Strip scripts, iframes, and potentially dangerous HTML
   - Preserve basic formatting (bold, italic, links, line breaks, paragraphs)
   - Create `IEmailBodySanitizer` interface and implement it

4. **Error handling:**
   - If processing a single email fails, log the error and continue to the next email (don't stop the batch)
   - If the Graph API call itself fails (auth, network), log and throw so Hangfire retries the job
   - Use `Polly` for transient fault handling on Graph API calls (retry 3 times with exponential backoff)

---

## Task 3.3 — Inbound Attachment Processing

### Instructions

1. When an inbound email has `hasAttachments == true`:
   - Call Graph API: `GET /users/{mailbox}/messages/{messageId}/attachments`
   - Filter to `#microsoft.graph.fileAttachment` type (skip inline/reference attachments)
   - For each file attachment:
     - Validate file size against `StorageSettings.MaxFileSizeMb`
     - Validate extension against `StorageSettings.AllowedExtensions`
     - Save via `IFileStorageService`
     - Create a `TicketAttachment` record linked to both the `Ticket` and the `TicketMessage`
   - If an attachment is rejected (too large, bad extension), log a warning and skip it — don't fail the email

2. Handle inline images:
   - If the email body references inline images (Content-ID), download and save them as regular attachments
   - Replace the `cid:` references in the HTML body with the stored file paths (or leave them as placeholders for v1)

---

## Task 3.4 — Outbound Email Service

### Instructions

1. **Create `IEmailSendingService`** in `Core/Interfaces/`:

```csharp
public interface IEmailSendingService
{
    Task<Result<string>> SendReplyAsync(int ticketId, int messageId, ReplySenderType senderType);
}
```

2. **Implement `EmailSendingService`** in `Infrastructure/Email/`:

   **Flow for `SendReplyAsync`:**

   ```
   1. Load the Ticket (with Company) and the TicketMessage
   2. Determine the sender:
      - If ReplySenderType.SharedMailbox → send from Company.SharedMailboxAddress
      - If ReplySenderType.AgentPersonal → send from the current agent's email
   3. Build the email:
      - To: Ticket.RequesterEmail
      - Subject: "RE: {originalSubject} [SH-{ticketId}]"
      - Body: TicketMessage.Body (wrap in a simple HTML template with basic styling)
      - Custom headers:
        - X-SupportHub-TicketId: {ticketId}
      - Attach any TicketAttachments linked to this message
   4. Send via Graph API:
      - If SharedMailbox: POST /users/{sharedMailboxAddress}/sendMail
      - If AgentPersonal: POST /users/{agentEmail}/sendMail
   5. Store the Graph message ID in TicketMessage.ExternalMessageId for threading
   6. If this is the first outbound message and Ticket.FirstResponseAt is null:
      - Set Ticket.FirstResponseAt = DateTimeOffset.UtcNow
   ```

3. **Email template:**
   - Create a simple HTML template stored as an embedded resource or in configuration
   - Include: agent's reply body, a footer with "Ticket Reference: SH-{ticketId}" and a note not to change the subject line
   - Keep it clean and simple — no heavy branding for v1

4. **Error handling:**
   - If Graph API send fails, return `Result.Failure` with the error message
   - The UI should show the error to the agent so they can retry
   - Do NOT delete the TicketMessage on send failure — the message is saved first, send is a separate step

---

## Task 3.5 — Integrate Outbound Email into Reply Flow

### Instructions

1. **Modify the ticket reply flow** (from Phase 2):

   When an agent submits a reply on a ticket that originated from email (or any ticket with a requester email):

   ```
   1. Save the TicketMessage (this already works from Phase 2)
   2. If the ticket has a requester email AND the agent chose to send as email:
      a. Call IEmailSendingService.SendReplyAsync
      b. If send succeeds, show success notification
      c. If send fails, show error but keep the saved message (agent can retry send)
   3. Update ticket status:
      - If status was AwaitingAgent or New → change to AwaitingCustomer
   ```

2. **Add a "Send as Email" toggle** to the reply composer (from Phase 2, Task 2.6):
   - Default to ON for email-sourced tickets
   - Default to OFF for portal-sourced tickets
   - When ON, show the sender selection (shared mailbox vs personal)
   - When OFF, the reply is saved as an internal message only (visible in portal but not emailed)

3. **Add a "Resend Email" button** on outbound messages that failed to send:
   - Only visible if `ExternalMessageId` is null (meaning send was never confirmed)
   - Clicking it calls `SendReplyAsync` again

---

## Task 3.6 — Hangfire Background Jobs

### Instructions

1. **Configure Hangfire** in `SupportHub.Web/Program.cs`:

```csharp
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "hangfire",
        PrepareSchemaIfNecessary = true
    }));
builder.Services.AddHangfireServer();
```

2. **Create `EmailPollingJob`** in `Infrastructure/Email/`:

```csharp
public class EmailPollingJob
{
    private readonly ICompanyService _companyService;
    private readonly IEmailIngestionService _emailIngestionService;
    private readonly ILogger<EmailPollingJob> _logger;

    // Constructor with DI

    public async Task ExecuteAsync()
    {
        var companies = await _companyService.GetAllActiveAsync();
        foreach (var company in companies)
        {
            try
            {
                await _emailIngestionService.ProcessInboxAsync(company.Id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inbox for company {CompanyId} ({CompanyName})", 
                    company.Id, company.Name);
                // Continue to next company — don't let one failure stop all processing
            }
        }
    }
}
```

3. **Register the recurring job** at startup:

```csharp
app.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<EmailPollingJob>(
        "email-polling",
        job => job.ExecuteAsync(),
        $"*/{settings.PollingIntervalSeconds / 60} * * * *"); // e.g., every 1 minute
```

4. **Protect the Hangfire dashboard:**
   - Mount at `/hangfire`
   - Restrict to `SuperAdmin` role via a custom `IDashboardAuthorizationFilter`

---

## Task 3.7 — Email Processing Monitoring

### Instructions

1. **Create `EmailProcessingLog` entity** (add to `Core/Entities/`):
   - `int CompanyId` (FK)
   - `DateTimeOffset ProcessedAt`
   - `int EmailsFound`
   - `int TicketsCreated`
   - `int MessagesAppended`
   - `int EmailsSkipped`
   - `int Errors`
   - `string? ErrorDetails` (nvarchar(max), JSON array of error messages)

2. **Update `EmailIngestionService`** to write a log record after each batch.

3. **Create a simple monitoring page** `Pages/Admin/EmailMonitoring.razor`:
   - Show the last 50 processing logs in a table
   - Columns: Company, Processed At, Emails Found, Tickets Created, Messages Appended, Skipped, Errors
   - Highlight rows with errors in red
   - Show "Last Successful Poll" per company at the top
   - Accessible to Admin/SuperAdmin only

---

## Task 3.8 — Testing

### Instructions

1. **Unit tests for `EmailIngestionService`:**
   - Mock `GraphServiceClient` responses
   - Test: new email creates a ticket
   - Test: reply email matches existing ticket by subject pattern
   - Test: reply email matches by custom header
   - Test: duplicate email is skipped (same ExternalMessageId)
   - Test: ignored sender patterns are skipped
   - Test: customer reply reopens a resolved ticket
   - Test: customer reply changes status from AwaitingCustomer to AwaitingAgent

2. **Unit tests for `EmailSendingService`:**
   - Test: reply from shared mailbox
   - Test: reply from agent personal email
   - Test: FirstResponseAt is set on first outbound message
   - Test: send failure returns Result.Failure (doesn't throw)

3. **Integration test (manual, documented in `docs/EmailTestingGuide.md`):**
   - Steps to send a test email to a shared mailbox
   - Steps to verify the ticket was created
   - Steps to reply from the UI and verify the email was received
   - Steps to reply by email and verify the message was appended

---

## Acceptance Criteria for Phase 3

- [ ] Emails sent to a company's shared mailbox automatically create tickets
- [ ] Reply emails are correctly matched to existing tickets (by header, subject pattern, or threading)
- [ ] Duplicate emails are not processed twice
- [ ] Email attachments are saved and linked to tickets
- [ ] Agents can reply from the UI and the customer receives an email
- [ ] Agents can choose to send from the shared mailbox or their personal email
- [ ] The ticket subject includes the `[SH-{id}]` reference for threading
- [ ] Customer email replies change ticket status appropriately (reopen, awaiting agent)
- [ ] First outbound message sets `FirstResponseAt` on the ticket
- [ ] Hangfire polls all active company mailboxes on the configured interval
- [ ] Email processing errors for one company don't block other companies
- [ ] Email monitoring page shows processing history with error visibility
- [ ] Hangfire dashboard is accessible only to SuperAdmins
- [ ] All new services have unit tests
