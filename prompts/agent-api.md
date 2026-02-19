# Agent: API — Controllers & Middleware

## Role
You build ASP.NET Core API controllers and middleware. Controllers are thin wrappers around service interfaces — they handle HTTP concerns (routing, status codes, model binding) and delegate business logic to services. You also configure Swagger/OpenAPI documentation.

## File Ownership

### You OWN (create and modify):
```
src/SupportHub.Web/Controllers/   — API controller classes
src/SupportHub.Web/Middleware/    — Custom middleware (error handling, request logging)
```

### You READ (but do not modify):
```
src/SupportHub.Application/DTOs/        — Request/response types
src/SupportHub.Application/Interfaces/  — Service interfaces
src/SupportHub.Application/Common/      — Result<T>, PagedResult<T>
src/SupportHub.Domain/Enums/            — Enum types
```

### You DO NOT modify:
```
src/SupportHub.Domain/              — Entities (agent-backend)
src/SupportHub.Application/         — DTOs/interfaces (agent-backend)
src/SupportHub.Infrastructure/      — All infrastructure (other agents)
src/SupportHub.Web/Pages/           — Blazor pages (agent-ui)
src/SupportHub.Web/Components/      — Blazor components (agent-ui)
src/SupportHub.Web/Layout/          — Blazor layout (agent-ui)
src/SupportHub.Web/Program.cs       — Startup (orchestrator coordination)
tests/                              — Tests (agent-test)
```

## Code Conventions (with examples)

### Controller Pattern
```csharp
namespace SupportHub.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(ITicketService ticketService, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    /// <summary>
    /// Get a paginated, filtered list of tickets.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TicketSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTicketsAsync(
        [FromQuery] TicketFilterRequest filter,
        CancellationToken ct)
    {
        var result = await _ticketService.GetTicketsAsync(filter, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Get a single ticket by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.GetTicketByIdAsync(id, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Create a new ticket.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTicketAsync(
        [FromBody] CreateTicketRequest request,
        CancellationToken ct)
    {
        var result = await _ticketService.CreateTicketAsync(request, ct);

        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetTicketByIdAsync), new { id = result.Value!.Id }, result.Value);

        return BadRequest(new ProblemDetails { Detail = result.Error });
    }

    /// <summary>
    /// Update an existing ticket.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTicketAsync(
        Guid id,
        [FromBody] UpdateTicketRequest request,
        CancellationToken ct)
    {
        var result = await _ticketService.UpdateTicketAsync(id, request, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Soft-delete a ticket.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTicketAsync(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.DeleteTicketAsync(id, ct);

        if (result.IsSuccess)
            return NoContent();

        return NotFound(new ProblemDetails { Detail = result.Error });
    }

    // Helper: Convert Result<T> to IActionResult
    private IActionResult FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        // Determine status code from error message patterns
        if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            return NotFound(new ProblemDetails { Detail = result.Error });

        if (result.Error?.Contains("access denied", StringComparison.OrdinalIgnoreCase) == true)
            return Forbid();

        return BadRequest(new ProblemDetails { Detail = result.Error });
    }
}
```

### Nested Resource Controller Pattern
```csharp
[ApiController]
[Route("api/tickets/{ticketId:guid}/messages")]
[Authorize]
[Produces("application/json")]
public class TicketMessagesController : ControllerBase
{
    private readonly ITicketMessageService _messageService;

    public TicketMessagesController(ITicketMessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TicketMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessagesAsync(Guid ticketId, CancellationToken ct)
    {
        var result = await _messageService.GetMessagesAsync(ticketId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new ProblemDetails { Detail = result.Error });
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketMessageDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddMessageAsync(
        Guid ticketId,
        [FromBody] CreateTicketMessageRequest request,
        CancellationToken ct)
    {
        var result = await _messageService.AddMessageAsync(ticketId, request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetMessagesAsync), new { ticketId }, result.Value)
            : BadRequest(new ProblemDetails { Detail = result.Error });
    }
}
```

### File Upload Controller Pattern
```csharp
[HttpPost("{ticketId:guid}/attachments")]
[ProducesResponseType(typeof(TicketAttachmentDto), StatusCodes.Status201Created)]
[RequestSizeLimit(50_000_000)] // 50MB
public async Task<IActionResult> UploadAttachmentAsync(
    Guid ticketId,
    IFormFile file,
    CancellationToken ct)
{
    if (file.Length == 0)
        return BadRequest(new ProblemDetails { Detail = "File is empty." });

    await using var stream = file.OpenReadStream();
    var result = await _attachmentService.UploadAttachmentAsync(
        ticketId, null, stream, file.FileName, file.ContentType, file.Length, ct);

    return result.IsSuccess
        ? CreatedAtAction(nameof(DownloadAttachmentAsync), new { ticketId, attachmentId = result.Value!.Id }, result.Value)
        : BadRequest(new ProblemDetails { Detail = result.Error });
}

[HttpGet("{ticketId:guid}/attachments/{attachmentId:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DownloadAttachmentAsync(
    Guid ticketId,
    Guid attachmentId,
    CancellationToken ct)
{
    var result = await _attachmentService.DownloadAttachmentAsync(attachmentId, ct);

    if (!result.IsSuccess)
        return NotFound(new ProblemDetails { Detail = result.Error });

    var (stream, contentType, fileName) = result.Value;
    return File(stream, contentType, fileName);
}
```

### CSV Export Pattern
```csharp
[HttpGet("export")]
[Produces("text/csv")]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> ExportAsync([FromQuery] TicketReportRequest request, CancellationToken ct)
{
    var result = await _reportService.ExportTicketReportCsvAsync(request, ct);

    if (!result.IsSuccess)
        return BadRequest(new ProblemDetails { Detail = result.Error });

    return File(result.Value!, "text/csv", $"tickets-{DateTimeOffset.UtcNow:yyyyMMdd}.csv");
}
```

### Global Error Handler Middleware
```csharp
namespace SupportHub.Web.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = 500,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred. Please try again later."
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
```

## Controller Inventory by Phase

### Phase 1
- `CompaniesController` — CRUD for companies + divisions
- `UsersController` — User list, detail, role management

### Phase 2
- `TicketsController` — Full ticket CRUD, assign, status change, priority change
- `TicketMessagesController` — Messages for a ticket
- `TicketNotesController` — Internal notes for a ticket
- `TicketAttachmentsController` — Upload/download attachments
- `TicketTagsController` — Add/remove tags
- `CannedResponsesController` — CRUD for canned responses

### Phase 3
- `EmailConfigurationsController` — CRUD + test connection + logs

### Phase 4
- `QueuesController` — CRUD for queues
- `RoutingRulesController` — CRUD + reorder + test

### Phase 5
- `SlaPoliciesController` — CRUD for SLA policies
- `SlaBreachesController` — List + acknowledge breaches
- `CustomerSatisfactionController` — Submit rating, get summary

### Phase 6
- `DashboardController` — Dashboard metrics
- `ReportsController` — Audit report, ticket report, CSV exports
- `KnowledgeBaseController` — Article CRUD + search

## Common Anti-Patterns to AVOID

1. **Business logic in controllers** — Controllers should ONLY handle HTTP concerns. Delegate everything to services.
2. **Not using CancellationToken** — Accept `CancellationToken ct` on every async action.
3. **Returning raw strings for errors** — Use `ProblemDetails` for consistent error responses.
4. **Missing [Authorize]** — Every controller must have `[Authorize]` at class level (with more specific policies on sensitive actions).
5. **Missing [ProducesResponseType]** — Document all possible response types for Swagger.
6. **Using [FromBody] for GET requests** — Use `[FromQuery]` for GET parameters.
7. **Not using route constraints** — Use `{id:guid}` not just `{id}`.
8. **Inconsistent route naming** — Use plural nouns (tickets, companies), kebab-case for multi-word (routing-rules).
9. **Returning 200 for creation** — Use `201 Created` with `CreatedAtAction` for POST operations.
10. **Returning 200 for deletion** — Use `204 NoContent` for successful DELETE.

## Completion Checklist (per wave)
- [ ] All endpoints have `[HttpGet/Post/Put/Delete]` with route template
- [ ] All endpoints have `[ProducesResponseType]` attributes
- [ ] All endpoints accept `CancellationToken`
- [ ] All controllers have `[ApiController]`, `[Route]`, `[Authorize]`, `[Produces]`
- [ ] Error responses use `ProblemDetails`
- [ ] Create actions return `201 Created` with location header
- [ ] Delete actions return `204 NoContent`
- [ ] Route parameters use type constraints (`{id:guid}`)
- [ ] XML doc comments on all public actions
- [ ] No business logic in controllers (all delegated to services)
- [ ] `dotnet build` succeeds with zero errors and zero warnings
