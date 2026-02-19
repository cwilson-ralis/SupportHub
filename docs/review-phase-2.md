# Code Review Report — Phase 2: Core Ticketing
**Date:** 2026-02-18
**Reviewer:** agent-reviewer
**Scope:** All Phase 2 files (Domain entities, Application DTOs/interfaces, Infrastructure services/configs, Web pages/controllers, Unit tests)

## Summary

Phase 2 Core Ticketing implementation demonstrates strong architectural discipline with clean separation of concerns, comprehensive security controls via company isolation, and well-structured Entity Framework configurations. All code follows established conventions with file-scoped namespaces, record-based DTOs, Result<T> pattern, and proper async/await patterns. Unit test coverage is solid with focused tests on success/failure paths, company isolation, and edge cases. No critical vulnerabilities identified. One high issue regarding attachment company isolation and two medium issues around path traversal validation and query efficiency were noted.

## Statistics

- **Files reviewed:** 47
- **Issues found:** 3
- **Critical:** 0 | **High:** 1 | **Medium:** 2 | **Low:** 0

---

## Critical Issues

None identified.

---

## High Issues

### H-1: AttachmentService Missing Company Isolation on `GetAttachmentsAsync`

- **File:** `src/SupportHub.Infrastructure/Services/AttachmentService.cs`
- **Line:** ~97–111
- **Description:** `GetAttachmentsAsync` retrieves attachments by ticket ID without validating company access. It checks that the ticket exists but does not call `HasAccessToCompanyAsync`, meaning an authenticated user who knows a ticket ID could enumerate attachments from tickets in other companies.
- **Impact:** An authenticated user from any company can call `GET /api/tickets/{id}/attachments/{attachmentId}` and retrieve attachment metadata (and file streams) from tickets they should not have access to.
- **Fix:** After loading the ticket, call `HasAccessToCompanyAsync(ticket.CompanyId, ct)` and return `Result.Failure("Access denied")` if it returns false.

---

## Medium Issues

### M-1: Path Traversal Not Validated on File Retrieval

- **File:** `src/SupportHub.Infrastructure/Services/LocalFileStorageService.cs`
- **Line:** ~45–52 (`GetFileAsync`, `DeleteFileAsync`)
- **Description:** `GetFileAsync` and `DeleteFileAsync` accept `storagePath` from the database and combine it with `_basePath` via `Path.Combine` without verifying the resolved path stays within `_basePath`. While paths are internally generated (and therefore trusted), a defence-in-depth check is missing.
- **Recommendation:** Add explicit path containment check before opening/deleting:
  ```csharp
  var fullPath = Path.Combine(_basePath, storagePath);
  var resolved = Path.GetFullPath(fullPath);
  if (!resolved.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
      return Result<...>.Failure("Invalid storage path");
  ```

### M-2: Redundant `.Include()` Before `.Select()` Projection in `GetTicketsAsync`

- **File:** `src/SupportHub.Infrastructure/Services/TicketService.cs`
- **Line:** ~113–116
- **Description:** The query loads `Company`, `AssignedAgent`, and `Tags` navigation properties via `.Include()` but then immediately uses a `.Select()` projection that only reads scalar properties (`.Name`, `.DisplayName`). EF Core will generate a single joined query either way, but the explicit `.Include()` calls add noise and may prevent EF from optimising the projection.
- **Recommendation:** Remove the three `.Include()` calls from the base query. The `.Select()` on lines 161–176 accesses the navigation properties directly and EF Core will generate the correct JOIN automatically. Retain `.Include(t => t.Tags)` only if the tag-filter branch (`filter.Tags`) is active, or move it inside the conditional.

---

## Low Issues

None identified.

---

## Positive Observations

- **Consistent company isolation:** `HasAccessToCompanyAsync()` is called at the start of every mutating and retrieval method across all 7 service classes — a strong and consistent enforcement pattern.
- **Status state machine:** The `ValidTransitions` dictionary in `TicketService` cleanly encodes all allowed status transitions. Invalid transitions return a descriptive `Result.Failure` message. Lifecycle timestamps (`ResolvedAt`, `ClosedAt`) are correctly set and cleared on reopen.
- **FirstResponseAt semantics correct:** `TicketMessageService.AddMessageAsync` sets `FirstResponseAt` only on the first outbound message (null check before assignment) and never overwrites a previously set value.
- **EF configurations thorough:** All 6 entity configurations apply `HasQueryFilter(x => !x.IsDeleted)`, use `HasConversion<string>()` for enums, define correct `DeleteBehavior.Restrict` on all FKs, and include appropriate indexes. The composite unique index on `(TicketId, Tag)` enforces deduplication at the DB level as a backstop to the service-layer check.
- **File upload security:** `AttachmentService` validates file size against a configurable maximum and validates extension against an explicit allowlist. Files are stored with a GUID prefix to prevent name collisions.
- **Ticket number generation:** Uses `IgnoreQueryFilters().OrderByDescending()` to find today's last number, making it safe even against soft-deleted tickets. The `D4` format and date prefix (`TKT-YYYYMMDD-NNNN`) are correct.
- **API authorization:** Both `TicketsController` and `CannedResponsesController` carry class-level `[Authorize]`. Blazor pages carry `@attribute [Authorize]`, with the CannedResponses admin page using `@attribute [Authorize(Policy = "Admin")]`.
- **Pagination at the database:** All list endpoints use `.Skip()` / `.Take()` before materialising results — no in-memory pagination.
- **Tag normalisation:** Tags are lowercased and trimmed consistently at write time in both `TicketService` (bulk tag creation) and `TagService` (individual add), with a case-insensitive duplicate check in the service layer backed by the DB unique index.
- **Test quality:** 48 new unit tests covering success paths, failure paths, company isolation, soft-delete verification (via `IgnoreQueryFilters()`), edge cases (duplicate tags differing only by case, subsequent outbound messages not overwriting `FirstResponseAt`), and ordering assertions.
- **CannedResponse scoping correct:** `null` companyId returns global-only responses; a supplied companyId returns company-specific plus global — matching the design spec exactly.

---

## Convention Compliance Score

| Dimension | Score | Notes |
|---|---|---|
| Convention Compliance | 5/5 | File-scoped namespaces, record DTOs, Result<T> throughout, no data annotations on entities, Async suffix, DateTimeOffset, Guid PKs — all consistent. |
| Architecture | 5/5 | Clean dependency direction. No business logic leaking into controllers or Blazor pages. |
| Security | 4/5 | Strong isolation pattern across all services. H-1 (GetAttachmentsAsync missing access check) and M-1 (path traversal defence-in-depth) are the only gaps. |
| Data Integrity | 5/5 | Correct DeleteBehavior.Restrict on all FKs, unique constraints present, MaxLength on all strings, soft-delete filters on every config, audit logging on CUD operations. |
| Performance | 4/5 | AsNoTracking on reads, DB-level pagination, projection via Select. M-2 (redundant Include before Select) is the only inefficiency. |
| Error Handling | 5/5 | IsSuccess checked before Value access in all controllers and Blazor components, loading states present, Snackbar error display, CancellationToken propagated throughout. |
| Test Coverage | 5/5 | All 7 services tested, both happy-path and failure cases, company isolation, soft-delete, edge cases. 82 total tests (34 Phase 1 + 48 Phase 2), all passing. |
| **Overall** | **4.7/5** | Excellent delivery. No critical issues. Address H-1 before exposing the attachment API externally. M-1 and M-2 are straightforward improvements. |
