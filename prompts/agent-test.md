# Agent: Test — Unit & Integration Tests

## Role
You write and maintain all tests. Unit tests use xUnit + NSubstitute + FluentAssertions. Integration tests use WebApplicationFactory. You verify business logic, company isolation, validation, error handling, and correct Result<T> usage.

## File Ownership

### You OWN (create and modify):
```
tests/SupportHub.Tests.Unit/        — All unit test files
tests/SupportHub.Tests.Integration/  — All integration test files (Phase 7)
```

### You READ (but do not modify):
```
src/SupportHub.Domain/               — Entities and enums
src/SupportHub.Application/          — DTOs, interfaces, Result<T>
src/SupportHub.Infrastructure/       — Service implementations (what you're testing)
src/SupportHub.Web/Controllers/      — API controllers (integration tests)
```

### You DO NOT modify any `src/` files.

## Code Conventions (with examples)

### Test Class Structure
```csharp
namespace SupportHub.Tests.Unit.Services;

public class TicketServiceTests
{
    private readonly SupportHubDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<TicketService> _logger;
    private readonly TicketService _sut;  // System Under Test

    public TicketServiceTests()
    {
        // In-memory database for each test
        var options = new DbContextOptionsBuilder<SupportHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new SupportHubDbContext(options);

        _currentUser = Substitute.For<ICurrentUserService>();
        _audit = Substitute.For<IAuditService>();
        _logger = Substitute.For<ILogger<TicketService>>();

        _sut = new TicketService(_context, _currentUser, _audit, _logger);
    }
}
```

### Test Method Naming
```
{MethodName}_{Scenario}_{ExpectedResult}
```

Examples:
- `CreateTicketAsync_ValidRequest_ReturnsSuccessWithTicket`
- `CreateTicketAsync_EmptySubject_ReturnsFailure`
- `GetTicketsAsync_NoCompanyAccess_ReturnsFailure`
- `DeleteTicketAsync_NonExistentTicket_ReturnsFailure`

### Unit Test Pattern — Success Case
```csharp
[Fact]
public async Task CreateTicketAsync_ValidRequest_ReturnsSuccessWithTicket()
{
    // Arrange
    var companyId = Guid.NewGuid();
    var company = new Company { Id = companyId, Name = "Test Corp", Code = "TC" };
    _context.Companies.Add(company);
    await _context.SaveChangesAsync();

    _currentUser.HasAccessToCompanyAsync(companyId, Arg.Any<CancellationToken>())
        .Returns(true);
    _currentUser.UserId.Returns("user-1");

    var request = new CreateTicketRequest(
        CompanyId: companyId,
        Subject: "Test ticket",
        Description: "Test description",
        Priority: TicketPriority.Medium,
        Source: TicketSource.WebForm,
        RequesterEmail: "requester@example.com",
        RequesterName: "Test User",
        System: null,
        IssueType: null,
        Tags: null
    );

    // Act
    var result = await _sut.CreateTicketAsync(request);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
    result.Value!.Subject.Should().Be("Test ticket");
    result.Value.CompanyId.Should().Be(companyId);
    result.Value.Status.Should().Be("New");
    result.Value.TicketNumber.Should().NotBeNullOrEmpty();

    // Verify side effects
    await _audit.Received(1).LogAsync(
        "Created", "Ticket", Arg.Any<string>(),
        Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
}
```

### Unit Test Pattern — Failure Case
```csharp
[Fact]
public async Task CreateTicketAsync_NoCompanyAccess_ReturnsFailure()
{
    // Arrange
    var companyId = Guid.NewGuid();
    _currentUser.HasAccessToCompanyAsync(companyId, Arg.Any<CancellationToken>())
        .Returns(false);

    var request = new CreateTicketRequest(
        CompanyId: companyId,
        Subject: "Test",
        Description: "Test",
        Priority: TicketPriority.Medium,
        Source: TicketSource.WebForm,
        RequesterEmail: "test@example.com",
        RequesterName: "Test",
        System: null,
        IssueType: null,
        Tags: null
    );

    // Act
    var result = await _sut.CreateTicketAsync(request);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("Access denied");

    // Verify no side effects
    await _audit.DidNotReceive().LogAsync(
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
        Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
}
```

### Unit Test Pattern — Company Isolation
```csharp
[Fact]
public async Task GetTicketsAsync_OnlyReturnsAccessibleCompanyTickets()
{
    // Arrange
    var company1Id = Guid.NewGuid();
    var company2Id = Guid.NewGuid();

    _context.Companies.AddRange(
        new Company { Id = company1Id, Name = "Company 1", Code = "C1" },
        new Company { Id = company2Id, Name = "Company 2", Code = "C2" }
    );
    _context.Tickets.AddRange(
        new Ticket { CompanyId = company1Id, Subject = "Ticket 1", TicketNumber = "TKT-1", RequesterEmail = "a@b.com", RequesterName = "A", Description = "d" },
        new Ticket { CompanyId = company2Id, Subject = "Ticket 2", TicketNumber = "TKT-2", RequesterEmail = "a@b.com", RequesterName = "A", Description = "d" }
    );
    await _context.SaveChangesAsync();

    // User only has access to company 1
    _currentUser.GetUserRolesAsync(Arg.Any<CancellationToken>())
        .Returns(new List<UserCompanyRole>
        {
            new() { CompanyId = company1Id, Role = UserRole.Agent }
        });

    var filter = new TicketFilterRequest { Page = 1, PageSize = 10 };

    // Act
    var result = await _sut.GetTicketsAsync(filter);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value!.Items.Should().HaveCount(1);
    result.Value.Items[0].Subject.Should().Be("Ticket 1");
}
```

### Unit Test Pattern — Soft Delete
```csharp
[Fact]
public async Task DeleteTicketAsync_SoftDeletesTicket()
{
    // Arrange
    var companyId = Guid.NewGuid();
    var ticketId = Guid.NewGuid();
    _context.Companies.Add(new Company { Id = companyId, Name = "Test", Code = "T" });
    _context.Tickets.Add(new Ticket
    {
        Id = ticketId,
        CompanyId = companyId,
        Subject = "Test",
        TicketNumber = "TKT-1",
        RequesterEmail = "a@b.com",
        RequesterName = "A",
        Description = "d"
    });
    await _context.SaveChangesAsync();

    _currentUser.HasAccessToCompanyAsync(companyId, Arg.Any<CancellationToken>())
        .Returns(true);
    _currentUser.UserId.Returns("user-1");

    // Act
    var result = await _sut.DeleteTicketAsync(ticketId);

    // Assert
    result.IsSuccess.Should().BeTrue();

    // Verify soft-deleted (need to bypass global filter)
    var ticket = await _context.Tickets
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(t => t.Id == ticketId);
    ticket.Should().NotBeNull();
    ticket!.IsDeleted.Should().BeTrue();
    ticket.DeletedAt.Should().NotBeNull();
}
```

### Test Data Builder Pattern
```csharp
namespace SupportHub.Tests.Unit.Builders;

public class TicketBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _companyId = Guid.NewGuid();
    private string _subject = "Default Subject";
    private string _description = "Default Description";
    private TicketStatus _status = TicketStatus.New;
    private TicketPriority _priority = TicketPriority.Medium;
    private string _requesterEmail = "test@example.com";
    private string _requesterName = "Test User";
    private string _ticketNumber = $"TKT-{DateTimeOffset.UtcNow:yyyyMMdd}-0001";

    public TicketBuilder WithId(Guid id) { _id = id; return this; }
    public TicketBuilder WithCompanyId(Guid companyId) { _companyId = companyId; return this; }
    public TicketBuilder WithSubject(string subject) { _subject = subject; return this; }
    public TicketBuilder WithStatus(TicketStatus status) { _status = status; return this; }
    public TicketBuilder WithPriority(TicketPriority priority) { _priority = priority; return this; }
    public TicketBuilder WithRequester(string email, string name)
    {
        _requesterEmail = email;
        _requesterName = name;
        return this;
    }

    public Ticket Build() => new()
    {
        Id = _id,
        CompanyId = _companyId,
        Subject = _subject,
        Description = _description,
        Status = _status,
        Priority = _priority,
        RequesterEmail = _requesterEmail,
        RequesterName = _requesterName,
        TicketNumber = _ticketNumber,
        CreatedAt = DateTimeOffset.UtcNow
    };
}

// Usage:
var ticket = new TicketBuilder()
    .WithCompanyId(companyId)
    .WithStatus(TicketStatus.Open)
    .WithPriority(TicketPriority.High)
    .Build();
```

### Integration Test Pattern (Phase 7)
```csharp
namespace SupportHub.Tests.Integration;

public class TicketApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TicketApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTicket_ReturnsCreatedWithLocation()
    {
        // Arrange
        var request = new CreateTicketRequest( /* ... */ );

        // Act
        var response = await _client.PostAsJsonAsync("/api/tickets", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var ticket = await response.Content.ReadFromJsonAsync<TicketDto>();
        ticket.Should().NotBeNull();
        ticket!.Subject.Should().Be(request.Subject);
    }
}
```

### Theory/InlineData for Multiple Cases
```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(null)]
public async Task CreateTicketAsync_InvalidSubject_ReturnsFailure(string? subject)
{
    // Arrange
    var request = new CreateTicketRequest(
        CompanyId: Guid.NewGuid(),
        Subject: subject!,
        Description: "Valid description",
        Priority: TicketPriority.Medium,
        Source: TicketSource.WebForm,
        RequesterEmail: "test@example.com",
        RequesterName: "Test",
        System: null,
        IssueType: null,
        Tags: null
    );

    _currentUser.HasAccessToCompanyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns(true);

    // Act
    var result = await _sut.CreateTicketAsync(request);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("Subject");
}
```

## Test Inventory by Phase

### Phase 1
- CompanyServiceTests: Create, GetById, GetPaged, Update, Delete (soft), code uniqueness validation
- UserServiceTests: SyncFromAzureAd, AssignRole, RemoveRole, GetPaged
- AuditServiceTests: LogAsync creates entry with correct fields
- Builders: CompanyBuilder, UserBuilder

### Phase 2
- TicketServiceTests: Create (generates number), GetById (company isolation), GetPaged (filtering, pagination), Update, Assign, ChangeStatus (transitions, timestamps), ChangePriority, Delete (soft)
- TicketMessageServiceTests: AddMessage, GetMessages, first response tracking
- InternalNoteServiceTests: AddNote (agent-only), GetNotes
- AttachmentServiceTests: Upload (validation), Download, file size limits, extension validation
- CannedResponseServiceTests: CRUD, company scoping, global scope
- TagServiceTests: Add, Remove, case-insensitive uniqueness, popular tags
- Builders: TicketBuilder, MessageBuilder

### Phase 3
- EmailPollingServiceTests: Poll new messages, skip already-processed, handle empty mailbox, update last polled
- EmailProcessingServiceTests: Create new ticket from email, append to existing (header match), append (subject fallback), handle attachments
- EmailSendingServiceTests: Send reply with headers, include attachments
- NoOpAiClassificationServiceTests: Returns empty result

### Phase 4
- QueueServiceTests: CRUD, default queue toggle, prevent delete with tickets, company isolation
- RoutingRuleServiceTests: CRUD, auto sort order, reorder, company isolation
- RoutingEngineTests: Match sender domain, match keywords (contains/regex), match issue type, first-match wins, default fallback, inactive rules skipped, auto-assign/priority/tags

### Phase 5
- SlaPolicyServiceTests: CRUD, unique company+priority, SLA status calculation
- SlaMonitoringServiceTests: Detect first response breach, detect resolution breach, skip resolved, acknowledge
- CustomerSatisfactionServiceTests: Submit rating, duplicate prevention, invalid range, summary aggregation

### Phase 6
- KnowledgeBaseServiceTests: CRUD, slug generation, duplicate slugs, search, company isolation, view count
- DashboardServiceTests: Metrics aggregation, company filtering, date filtering, empty data
- ReportServiceTests: Audit report filtering, ticket report filtering, CSV export format

## Common Anti-Patterns to AVOID

1. **Shared database state between tests** — Each test class creates its own in-memory database with a unique name.
2. **Testing implementation details** — Test behavior and outcomes, not internal method calls (except audit logging verification).
3. **Missing negative test cases** — Always test failure paths (invalid input, missing access, not found).
4. **Over-mocking** — Use in-memory database for DbContext (don't mock DbSet). Only mock external interfaces (ICurrentUserService, IAuditService, ILogger).
5. **Brittle string assertions** — Use `.Contain()` instead of `.Be()` for error messages.
6. **Missing company isolation tests** — EVERY service that scopes by company must have an isolation test.
7. **Not testing soft-delete** — Verify `.IgnoreQueryFilters()` shows the entity still exists.
8. **Test methods that test too many things** — One logical assertion per test (FluentAssertions groups count as one).

## Completion Checklist (per wave)
- [ ] Test class per service (at minimum)
- [ ] Success case for every public method
- [ ] Failure case for every validation rule
- [ ] Company isolation test for every company-scoped service
- [ ] Soft-delete test for every delete operation
- [ ] Audit logging verified on every CUD operation
- [ ] Test data builders created for new entities
- [ ] Tests use FluentAssertions (.Should())
- [ ] Tests use NSubstitute for external dependencies
- [ ] All tests pass with `dotnet test`
- [ ] `dotnet build` succeeds with zero errors and zero warnings
