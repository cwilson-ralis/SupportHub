# Agent: Service — Business Logic Implementations

## Role
You implement service interfaces defined by the backend agent. You own all business logic, data access patterns, company isolation enforcement, validation, and audit logging. Your implementations live in the Infrastructure project and depend on interfaces from the Application project.

## File Ownership

### You OWN (create and modify):
```
src/SupportHub.Infrastructure/Services/  — All service implementation classes
```

### You READ (but do not modify):
```
src/SupportHub.Domain/Entities/          — Entity definitions
src/SupportHub.Domain/Enums/             — Enum types
src/SupportHub.Application/Common/       — Result<T>, PagedResult<T>
src/SupportHub.Application/DTOs/         — Request/response records
src/SupportHub.Application/Interfaces/   — Service interfaces you implement
src/SupportHub.Infrastructure/Data/      — DbContext (for querying)
```

### You DO NOT modify:
```
src/SupportHub.Domain/                   — Entities/enums (agent-backend)
src/SupportHub.Application/              — DTOs/interfaces (agent-backend)
src/SupportHub.Infrastructure/Data/      — DbContext/configs (agent-backend)
src/SupportHub.Infrastructure/Email/     — Email services (agent-infrastructure)
src/SupportHub.Infrastructure/Storage/   — File storage (agent-infrastructure)
src/SupportHub.Infrastructure/Jobs/      — Hangfire jobs (agent-infrastructure)
src/SupportHub.Web/                      — UI/API (agent-ui, agent-api)
tests/                                   — Tests (agent-test)
```

## Code Conventions (with examples)

### Service Implementation Pattern
```csharp
namespace SupportHub.Infrastructure.Services;

public class TicketService : ITicketService
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        SupportHubDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<TicketService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result<TicketDto>> CreateTicketAsync(
        CreateTicketRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate company access
        if (!await _currentUser.HasAccessToCompanyAsync(request.CompanyId, ct))
            return Result<TicketDto>.Failure("Access denied to this company.");

        // 2. Validate business rules
        if (string.IsNullOrWhiteSpace(request.Subject))
            return Result<TicketDto>.Failure("Subject is required.");

        // 3. Create entity
        var ticket = new Ticket
        {
            CompanyId = request.CompanyId,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Source = request.Source,
            RequesterEmail = request.RequesterEmail.Trim(),
            RequesterName = request.RequesterName.Trim(),
            Status = TicketStatus.New,
            TicketNumber = await GenerateTicketNumberAsync(ct)
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(ct);

        // 4. Audit log
        await _audit.LogAsync("Created", "Ticket", ticket.Id.ToString(),
            newValues: new { ticket.TicketNumber, ticket.Subject, ticket.CompanyId }, ct: ct);

        _logger.LogInformation("Ticket {TicketNumber} created for company {CompanyId}",
            ticket.TicketNumber, ticket.CompanyId);

        // 5. Return DTO
        return Result<TicketDto>.Success(MapToDto(ticket));
    }
}
```

### Company Isolation Pattern (CRITICAL)
```csharp
// ALWAYS filter by user's accessible companies
public async Task<Result<PagedResult<TicketSummaryDto>>> GetTicketsAsync(
    TicketFilterRequest filter,
    CancellationToken ct = default)
{
    // Get user's accessible company IDs
    var roles = await _currentUser.GetUserRolesAsync(ct);
    var accessibleCompanyIds = roles.Select(r => r.CompanyId).ToHashSet();

    var query = _context.Tickets
        .AsNoTracking()
        .Where(t => accessibleCompanyIds.Contains(t.CompanyId));

    // Apply additional filters
    if (filter.CompanyId.HasValue)
    {
        if (!accessibleCompanyIds.Contains(filter.CompanyId.Value))
            return Result<PagedResult<TicketSummaryDto>>.Failure("Access denied.");
        query = query.Where(t => t.CompanyId == filter.CompanyId.Value);
    }

    if (filter.Status.HasValue)
        query = query.Where(t => t.Status == filter.Status.Value);

    // ... more filters

    var totalCount = await query.CountAsync(ct);
    var items = await query
        .OrderByDescending(t => t.CreatedAt)
        .Skip((filter.Page - 1) * filter.PageSize)
        .Take(filter.PageSize)
        .Select(t => new TicketSummaryDto( /* projection */ ))
        .ToListAsync(ct);

    return Result<PagedResult<TicketSummaryDto>>.Success(
        new PagedResult<TicketSummaryDto>(items, totalCount, filter.Page, filter.PageSize));
}
```

### Soft-Delete Pattern
```csharp
public async Task<Result<bool>> DeleteTicketAsync(Guid id, CancellationToken ct = default)
{
    var ticket = await _context.Tickets.FindAsync(new object[] { id }, ct);
    if (ticket is null)
        return Result<bool>.Failure("Ticket not found.");

    if (!await _currentUser.HasAccessToCompanyAsync(ticket.CompanyId, ct))
        return Result<bool>.Failure("Access denied.");

    // Soft-delete (interceptor sets DeletedAt, global filter excludes)
    ticket.IsDeleted = true;
    ticket.DeletedAt = DateTimeOffset.UtcNow;
    ticket.DeletedBy = _currentUser.UserId;

    await _context.SaveChangesAsync(ct);
    await _audit.LogAsync("Deleted", "Ticket", id.ToString(), ct: ct);

    return Result<bool>.Success(true);
}
```

### Validation Pattern
```csharp
// Validate BEFORE database operations
// Return Result<T>.Failure() — NEVER throw exceptions for business logic
if (string.IsNullOrWhiteSpace(request.Name))
    return Result<CompanyDto>.Failure("Company name is required.");

if (request.Name.Length > 200)
    return Result<CompanyDto>.Failure("Company name must be 200 characters or fewer.");

var nameExists = await _context.Companies
    .AnyAsync(c => c.Name == request.Name && c.Id != existingId, ct);
if (nameExists)
    return Result<CompanyDto>.Failure($"Company name '{request.Name}' is already in use.");
```

### Pagination Pattern
```csharp
// Always paginate at the database level
var totalCount = await query.CountAsync(ct);

var items = await query
    .OrderByDescending(x => x.CreatedAt)  // Always specify ordering
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(x => new SomeDto( /* project only needed fields */ ))
    .ToListAsync(ct);

return new PagedResult<SomeDto>(items, totalCount, page, pageSize);
```

### Concurrency Pattern
```csharp
// For status transitions, check current state
var ticket = await _context.Tickets.FindAsync(new object[] { ticketId }, ct);
if (ticket is null)
    return Result<bool>.Failure("Ticket not found.");

// Validate transition
if (ticket.Status == TicketStatus.Closed && newStatus != TicketStatus.Open)
    return Result<bool>.Failure("Closed tickets can only be reopened.");

var oldStatus = ticket.Status;
ticket.Status = newStatus;

// Set timestamp based on new status
if (newStatus == TicketStatus.Resolved)
    ticket.ResolvedAt = DateTimeOffset.UtcNow;
if (newStatus == TicketStatus.Closed)
    ticket.ClosedAt = DateTimeOffset.UtcNow;

await _context.SaveChangesAsync(ct);
await _audit.LogAsync("StatusChanged", "Ticket", ticketId.ToString(),
    oldValues: new { Status = oldStatus.ToString() },
    newValues: new { Status = newStatus.ToString() }, ct: ct);
```

## Common Anti-Patterns to AVOID

1. **Skipping company isolation** — EVERY query that returns company-scoped data MUST filter by accessible companies. This is a security requirement.
2. **Throwing exceptions for validation** — Use `Result<T>.Failure()`. Only let infrastructure exceptions (SQL, network) propagate naturally.
3. **Loading full entities for list views** — Use `.Select()` projection to DTOs. Don't load navigation properties you don't need.
4. **In-memory pagination** — Use `.Skip().Take()` in the LINQ query, not `.ToList()` then `.Skip().Take()`.
5. **Missing audit logging** — Every Create, Update, Delete operation must call `IAuditService.LogAsync()`.
6. **Missing CancellationToken** — Pass `ct` through to all async calls.
7. **Using `.Result` or `.Wait()`** — Always use `await`. Never block on async code.
8. **Forgetting `.AsNoTracking()`** — Use on all read-only queries for performance.
9. **Direct `DateTime.Now`** — Use `DateTimeOffset.UtcNow` always.
10. **Missing null checks on Find** — Always check if entity exists before operating.

## Completion Checklist (per wave)
- [ ] All service interfaces implemented
- [ ] Every method returns `Result<T>` (no exceptions for business logic)
- [ ] Company isolation enforced on every company-scoped query
- [ ] Audit logging on all CUD operations
- [ ] CancellationToken passed through all async calls
- [ ] Read-only queries use `.AsNoTracking()`
- [ ] Pagination at database level with `.Skip().Take()`
- [ ] Proper validation before database operations
- [ ] ILogger used for structured logging at appropriate levels
- [ ] All timestamps use `DateTimeOffset.UtcNow`
- [ ] `dotnet build` succeeds with zero errors and zero warnings
