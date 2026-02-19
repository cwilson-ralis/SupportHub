# Code Review Report — Phase 3: Email Integration
**Date:** 2026-02-19
**Reviewer:** agent-reviewer
**Scope:** All Phase 3 files (Domain entities, Application DTOs/interfaces, Infrastructure services/configs/jobs, Web pages/controllers, Unit tests)

## Summary

Phase 3 email integration is well-structured with correct Graph API integration, robust email threading via `X-SupportHub-TicketId` headers, comprehensive de-duplication logic, and good Hangfire job scheduling. Conventions are generally well-followed across all 29 files. One critical company isolation bypass was identified in `EmailConfigurationService.GetLogsAsync`, and one high issue in the corresponding UI page. One medium defence-in-depth gap was noted in `EmailProcessingService`.

## Statistics

- **Files reviewed:** 29
- **Issues found:** 3
- **Critical:** 1 | **High:** 1 | **Medium:** 1 | **Low:** 0

---

## Critical Issues

### C-1: Company Isolation Bypass in `EmailConfigurationService.GetLogsAsync`

- **File:** `src/SupportHub.Infrastructure/Services/EmailConfigurationService.cs`
- **Line:** ~160–179
- **Description:** `GetLogsAsync` retrieves email processing logs by `emailConfigurationId` without verifying that the current user has access to the associated company. Any authenticated user who knows (or guesses) a configuration ID can retrieve logs from another company's email configuration.
- **Impact:** Cross-company data disclosure of email subjects, sender addresses, and ticket linkage data. Violates multi-tenancy isolation and SOX audit trail requirements.
- **Fix:** After loading the `EmailConfiguration` by ID, call `_currentUserService.HasAccessToCompanyAsync(config.CompanyId, ct)` and return `Result.Failure("Access denied.")` if it returns false — mirroring the pattern used in `GetByIdAsync`.

---

## High Issues

### H-1: `EmailLogs.razor` Loads Logs Without Company Isolation

- **File:** `src/SupportHub.Web/Components/Pages/Admin/EmailLogs.razor`
- **Line:** ~81–86 (`LoadAsync`)
- **Description:** The page iterates over all accessible configs and calls `GetLogsAsync` for each. Because the service-layer check is missing (C-1), an Admin from Company A can view logs from Company B's email configurations if they know the configuration ID. The `[Authorize(Policy = "Admin")]` guard only validates authentication and role, not company scope.
- **Recommendation:** The primary fix is C-1 in the service layer. Once that is in place this page will correctly receive `Access denied` for out-of-scope configs. No additional UI changes required beyond confirming C-1 is applied.

---

## Medium Issues

### M-1: `EmailProcessingService.ProcessInboundEmailAsync` Lacks Defence-in-Depth Company Check

- **File:** `src/SupportHub.Infrastructure/Services/EmailProcessingService.cs`
- **Line:** ~25–40
- **Description:** The method accepts an `emailConfigurationId` and loads the configuration without verifying the caller is authorised for that company. The current callers (EmailPollingJob, EmailPollingService) are internal and trusted, but if this method is ever exposed via a future API endpoint the absence of a company check becomes a vulnerability.
- **Recommendation:** This is a defence-in-depth concern, not an immediate vulnerability. Consider documenting the trust boundary assumption, or add a `companyId` parameter that callers must supply and validate against the loaded config.

---

## Low Issues

None identified.

---

## Positive Observations

- **Email threading priority correct:** `X-SupportHub-TicketId` header is checked before subject-line fallback in `EmailProcessingService`, ensuring reliable threading even when subjects change.
- **Robust de-duplication:** `ExternalMessageId` is checked against `EmailProcessingLog` before processing, preventing duplicate ticket creation on re-polls.
- **Outbound header set correctly:** `EmailSendingService` adds `X-SupportHub-TicketId` to every outbound message, maintaining thread continuity for replies.
- **Hangfire error isolation:** `EmailPollingJob` catches per-config failures individually and continues processing remaining configurations — one failing mailbox does not block others.
- **`AutoCreateTickets` flag respected:** `EmailProcessingService` records `"Skipped"` and returns `Success(null)` when the flag is false and no existing ticket is matched.
- **`LastPolledAt` updated on every poll:** `EmailPollingService` persists `DateTimeOffset.UtcNow` after each successful Graph query, enabling the per-config interval check in `EmailPollingJob`.
- **Comprehensive `EmailProcessingLog`:** Every inbound email is recorded with status (`Created`/`Appended`/`Skipped`/`Failed`), sender, subject, linked ticket ID, and error message — a strong audit trail.
- **Config-driven `GraphClientFactory`:** Reads `TenantId`, `ClientId`, and `ClientSecret` from `IConfiguration` — no hardcoded secrets.
- **`HangfireSuperAdminFilter` correct:** Dashboard at `/hangfire` is properly restricted to authenticated users in the `SuperAdmin` role.
- **Test quality:** 25 tests covering threading (header vs. subject), de-duplication, `AutoCreateTickets=false`, AI classification storage, attachment handling, Graph call failures, polling interval logic, and Hangfire job error isolation.
- **EF configuration thorough:** Unique index on `(CompanyId, SharedMailboxAddress)`, indexes on `ExternalMessageId`/`ProcessedAt`/`IsActive`, correct `DeleteBehavior.Restrict` and `SetNull` where appropriate.

---

## Convention Compliance Score

| Dimension | Score | Notes |
|---|---|---|
| Convention Compliance | 5/5 | File-scoped namespaces, record DTOs, Result&lt;T&gt; throughout, primary constructors, DateTimeOffset UTC, soft-delete, Async suffix — all consistent. |
| Architecture | 4/5 | Clean dependency direction. Minor: `ProcessInboundEmailAsync` lacks an explicit company scope parameter; trust boundary is implicit. |
| Security | 3/5 | C-1 (GetLogsAsync isolation bypass) is the only gap, but it is significant. All controllers carry `[Authorize]`; Hangfire dashboard is SuperAdmin-only; inbound webhook stub is safe. |
| Data Integrity | 5/5 | FK constraints with correct `DeleteBehavior`, unique composite index, `MaxLength` on all strings, `EmailProcessingLog` captures all outcomes. |
| Performance | 4/5 | Graph query capped at 50 messages per poll, indexes match query patterns. No pagination on `GetLogsAsync` (count param exists but is unbounded by default at 50 — acceptable). |
| Error Handling | 4/5 | `Result<T>` used throughout, per-config error isolation in `EmailPollingJob`, structured logging at appropriate levels. `GraphClientFactory` does not validate config keys before use (minor). |
| Test Coverage | 5/5 | All 6 new services and the Hangfire job are tested. Threading, de-dup, AI classification, attachments, guard clauses, and scheduling intervals all covered. 107 total tests, all passing. |
| **Overall** | **4.3/5** | Excellent delivery with a well-designed email pipeline. Address C-1 before exposing the logs endpoint externally. M-1 is a low-priority hardening item. |
