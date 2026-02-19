# Phase 4 - Rules Engine & Routing UI

> **Prerequisites:** Phases 0-3 complete. Divisions, RoutingRule entities, and TicketTag entities exist in the database (created in Phase 1). Tickets are being created via portal and email. Email AI classification is operational.

---

## Objective

Build an admin-configurable routing rules engine that automatically assigns incoming tickets to the correct Queue (Division) without developer intervention. Admins (including David, Jason, and Kim) must be able to create, edit, reorder, enable/disable rules, and manage queues through the UI - no code changes required. At the end of this phase, ticket routing is largely automatic.

This phase intentionally changes email routing order from Phase 3 AI-first behavior to rules-first behavior, with AI used only as fallback when no rule matches.

---

## Task 4.1 - Queue (Division) Management Service

### Instructions

1. **Create `IDivisionService`** in `Core/Interfaces/`:

```csharp
public interface IDivisionService
{
 Task<Result<List<DivisionDto>>> GetByCompanyIdAsync(int companyId);
 Task<Result<DivisionDto>> GetByIdAsync(int id);
 Task<Result<DivisionDto>> CreateAsync(CreateDivisionDto dto);
 Task<Result<DivisionDto>> UpdateAsync(int id, UpdateDivisionDto dto);
 Task<Result<bool>> DeactivateAsync(int id);
}
```

2. **Create DTOs:**
 - `DivisionDto`: Id, CompanyId, CompanyName, Name, Description, IsActive, TicketCount (current open tickets in this division)
 - `CreateDivisionDto`: CompanyId, Name, Description
 - `UpdateDivisionDto`: Name, Description

3. **Implement `DivisionService`:**
 - Only Admin/SuperAdmin can create/edit/deactivate
 - Cannot deactivate a division that has open tickets (return friendly error)
 - SuperAdmin sees divisions for all companies; Admin sees only their assigned companies

4. **Create Blazor page `Pages/Admin/Divisions.razor`:**
 - Company selector at top
 - List of divisions with: Name, Description, Active status toggle, Open ticket count
 - "Add Division" button opens inline form or dialog
 - Inline edit (name + description) with save button
 - "Deactivate" button with confirmation (warns if tickets exist)
 - Initial seed data for each company (suggested defaults): **Tech Support**, **App Support**, **New Hire / Termination**, **General**

---

## Task 4.2 - Routing Rule Service

### Instructions

1. **Create `IRoutingRuleService`** in `Core/Interfaces/`:

```csharp
public interface IRoutingRuleService
{
 Task<Result<List<RoutingRuleDto>>> GetByCompanyIdAsync(int companyId);
 Task<Result<RoutingRuleDto>> CreateAsync(CreateRoutingRuleDto dto);
 Task<Result<RoutingRuleDto>> UpdateAsync(int id, UpdateRoutingRuleDto dto);
 Task<Result<bool>> DeleteAsync(int id);
 Task<Result<bool>> ReorderAsync(int companyId, List<int> orderedIds); // reorder rules
 Task<Result<bool>> ToggleEnabledAsync(int id);
}
```

2. **Create DTOs:**

 - `RoutingRuleDto`: Id, CompanyId, Name, IsEnabled, SortOrder, ConditionType (enum + display name), ConditionValue, TargetDivisionId, TargetDivisionName, AutoTag
 - `CreateRoutingRuleDto`: CompanyId, Name, ConditionType, ConditionValue, TargetDivisionId?, AutoTag?
 - `UpdateRoutingRuleDto`: Name, ConditionType, ConditionValue, TargetDivisionId?, AutoTag?

3. **Implement `RoutingRuleService`:**
 - Only Admin/SuperAdmin can manage
 - `ReorderAsync`: accepts the full ordered list of IDs and updates `SortOrder` values
 - Validate: `ConditionValue` must not be empty, `TargetDivisionId` (if provided) must belong to the same company

4. **Unit tests:**
 - Test: create rule, verify sort order assigned
 - Test: reorder rules, verify new sort order persisted
 - Test: ConditionValue empty -> validation error
 - Test: TargetDivisionId from wrong company -> validation error

---

## Task 4.3 - Routing Rules Evaluation Pipeline

### Instructions

1. **Create `IRoutingEngine`** in `Core/Interfaces/`:

```csharp
public interface IRoutingEngine
{
 Task<RoutingResult> EvaluateAsync(RoutingContext context, CancellationToken cancellationToken = default);
}

public record RoutingContext(
 int CompanyId,
 string? SenderEmail,
 string Subject,
 string Body,
 string? SystemApplication, // from form; null for email submissions
 IssueType? IssueType, // from form; null for email submissions
 TicketSource Source);

public record RoutingResult(
 int? TargetDivisionId, // null = General queue (no match)
 List<string> TagsToApply,
 string? MatchedRuleName, // for logging
 bool Matched);
```

2. **Implement `RoutingEngine`** in `Infrastructure/Services/`:

 **Evaluation flow:**

 ```
 1. Load all enabled RoutingRules for the company, ordered by SortOrder ASC
 2. For each rule:
 a. Evaluate the rule condition against the RoutingContext:
 - SenderDomain: sender email domain matches ConditionValue (e.g., "@tle.com" -> domain suffix match)
 - SubjectKeyword: subject contains ConditionValue (case-insensitive)
 - BodyKeyword: body contains ConditionValue (case-insensitive)
 - FormSystemApplication: SystemApplication == ConditionValue (only applies to Portal/API source)
 - FormIssueType: IssueType.ToString() == ConditionValue (only applies to Portal/API source)
 b. If rule matches:
 - Set TargetDivisionId = rule.TargetDivisionId
 - Add rule.AutoTag to TagsToApply (if not null/empty)
 - Stop evaluation (first match wins)
 3. If no rule matches:
 - Return RoutingResult with TargetDivisionId = null, Matched = false
 ```

3. **Integrate routing engine into ticket creation flow:**

 In `TicketService.CreateAsync`:
 - After basic ticket creation, call `IRoutingEngine.EvaluateAsync` with the ticket's context
 - Apply the result: set `Ticket.DivisionId` and add any `TagsToApply` as `TicketTag` records
 - Log the routing result (rule matched or no match)

4. **Integrate routing engine into email ingestion:**

 In `EmailIngestionService` (Phase 3, Task 3.2):
 - After creating a new ticket from email, call `IRoutingEngine.EvaluateAsync` (this supersedes Phase 3 AI-first routing order)
 - If routing matches, apply result before AI classification step
 - If routing has no match, proceed to AI classification (Phase 3, Task 3.5)

5. **Unit tests for `RoutingEngine`:**
 - Test: SenderDomain rule matches on correct domain
 - Test: SenderDomain rule does not match on different domain
 - Test: SubjectKeyword match is case-insensitive
 - Test: FormSystemApplication only evaluates for Portal/API source tickets
 - Test: First matching rule wins (not subsequent rules)
 - Test: No matching rule returns Matched = false
 - Test: AutoTag is included in result when rule matches
 - Test: Multiple rules evaluated in SortOrder order

---

## Task 4.4 - Admin UI: Routing Rules Page

### Instructions

Create `Pages/Admin/RoutingRules.razor`:

1. **Layout:**
 - Company selector at top
 - Summary: "Rules are evaluated in order - first match wins. Unmatched tickets go to the General queue."
 - "Add Rule" button (opens dialog)
 - Sortable list/table of rules with drag-and-drop reordering (`MudDropContainer` or a simple up/down arrow pair)

2. **Rule list columns:**
 - Drag handle icon
 - Order number (1, 2, 3...)
 - Enabled toggle (`MudSwitch`)
 - Name
 - Condition (friendly display: e.g., "Sender domain is `@tle.com`", "Subject contains `Empower`", "Issue type is `Termination`")
 - Routes to (division name, or "General Queue" if no division)
 - Auto-tag (if set, show the tag chip)
 - Edit / Delete buttons

3. **Add/Edit Rule Dialog:**
 - Name (required)
 - Condition type (dropdown: Sender Email Domain, Subject Keyword, Body Keyword, System/Application, Issue Type)
 - Condition value (text - context hint changes based on condition type, e.g., "Enter domain, e.g. @tle.com")
 - Routes to Division (dropdown of company divisions, or "General Queue - no automatic assignment")
 - Auto-apply tag (text, optional - e.g., `termination`)
 - Preview: "If a ticket matches this rule, it will be routed to `{Division}` and tagged `{tag}`"

4. **Reorder behavior:**
 - Drag-and-drop updates the visual list immediately
 - "Save Order" button persists the new order via `ReorderAsync`
 - Or: auto-save on drop (with undo support via `MudSnackbar` "Undo" action)

---

## Task 4.5 - Admin UI: Queue Overview Page

### Instructions

Create `Pages/Admin/QueueOverview.razor` (accessible to Admin/SuperAdmin):

This page gives a real-time view of the current ticket load across all queues.

1. **Layout:**
 - Company selector at top
 - One card per active division showing:
 - Division name
 - Current open ticket count (by status breakdown: New, Open, AwaitingAgent, OnHold)
 - Unassigned ticket count (red badge if > 0)
 - Avg age of open tickets in this queue
 - "View Tickets" link -> navigates to ticket list filtered to this division
 - Plus a "General Queue" card for unrouted tickets
 - Refresh button + auto-refresh every 60 seconds

2. **Use `IReportingService`** (Phase 6) stub or a simple `IDivisionService` method that returns the stats.

---

## Task 4.6 - Testing

### Instructions

1. **Integration test scenario (manual, documented):**
 - Create a routing rule: sender domain `@test.com` -> Tech Support
 - Send an email from `@test.com` -> verify ticket routed to Tech Support
 - Create a routing rule: issue type `Termination` -> New Hire/Termination division, auto-tag `termination`
 - Submit a portal ticket with issue type Termination -> verify routing and tag
 - Disable the rule -> verify ticket falls through to next rule or General

2. **UI test scenarios (manual):**
 - Drag-and-drop reorder works and persists
 - Toggle enable/disable on a rule takes effect immediately for new tickets
 - Rule with no division routes to General queue

---

## Acceptance Criteria for Phase 4

- [ ] Admins can create, edit, enable/disable, and delete routing rules via the UI
- [ ] Rules can be reordered via drag-and-drop (or up/down arrows)
- [ ] Rule evaluation correctly routes portal-submitted tickets based on form fields
- [ ] Rule evaluation correctly routes email-submitted tickets based on sender domain and keywords
- [ ] First-match-wins behavior is enforced
- [ ] Unmatched tickets go to General (null DivisionId)
- [ ] Auto-tags from routing rules are applied to tickets
- [ ] AI classification is only invoked when routing rules yield no match (email source)
- [ ] Admins can manage divisions/queues (create, edit, deactivate) via the UI
- [ ] Queue overview page shows real-time ticket load per division
- [ ] All new services have unit tests

