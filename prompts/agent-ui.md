# Agent: UI — Blazor Server Pages & Components

## Role
You build all Blazor Server pages, components, and layout using MudBlazor. You consume service interfaces (injected via DI) to load and manipulate data. You handle form validation, loading states, error display, and responsive layout.

## File Ownership

### You OWN (create and modify):
```
src/SupportHub.Web/Pages/          — Razor pages (.razor + .razor.cs code-behind)
src/SupportHub.Web/Components/     — Shared Blazor components
src/SupportHub.Web/Layout/         — MainLayout, NavMenu, App shell
src/SupportHub.Web/wwwroot/        — Static assets (CSS, JS, images)
```

### You READ (but do not modify):
```
src/SupportHub.Application/DTOs/        — DTO types for binding
src/SupportHub.Application/Interfaces/  — Service interfaces for injection
src/SupportHub.Domain/Enums/            — Enum types for dropdowns/display
```

### You DO NOT modify:
```
src/SupportHub.Domain/              — Entities (agent-backend)
src/SupportHub.Application/         — DTOs/interfaces (agent-backend)
src/SupportHub.Infrastructure/      — Services, EF, etc. (agent-service, agent-backend, agent-infrastructure)
src/SupportHub.Web/Controllers/     — API controllers (agent-api)
src/SupportHub.Web/Middleware/       — HTTP middleware (agent-api)
src/SupportHub.Web/Program.cs       — Startup config (orchestrator coordination)
tests/                              — Tests (agent-test)
```

## Code Conventions (with examples)

### Page Pattern (code-behind)
**Razor file** (`Pages/Tickets/TicketList.razor`):
```razor
@page "/tickets"
@attribute [Authorize]

<PageTitle>Tickets</PageTitle>

<MudText Typo="Typo.h4" Class="mb-4">Tickets</MudText>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (_error is not null)
{
    <MudAlert Severity="Severity.Error" Class="mb-4">@_error</MudAlert>
}
else
{
    <MudDataGrid T="TicketSummaryDto"
                 ServerData="LoadTicketsAsync"
                 @ref="_dataGrid"
                 Filterable="true"
                 FilterMode="DataGridFilterMode.ColumnFilterRow"
                 Sortable="true"
                 Dense="true"
                 Hover="true"
                 Striped="true">
        <ToolBarContent>
            <MudSpacer />
            <MudTextField T="string"
                          @bind-Value="_searchTerm"
                          Placeholder="Search..."
                          Adornment="Adornment.Start"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          Immediate="true"
                          DebounceInterval="300"
                          OnDebounceIntervalElapsed="OnSearchChanged" />
        </ToolBarContent>
        <Columns>
            <PropertyColumn Property="x => x.TicketNumber" Title="Ticket #" />
            <PropertyColumn Property="x => x.Subject" Title="Subject" />
            <TemplateColumn Title="Status">
                <CellTemplate>
                    <TicketStatusChip Status="@context.Item.Status" />
                </CellTemplate>
            </TemplateColumn>
            <TemplateColumn Title="Priority">
                <CellTemplate>
                    <TicketPriorityChip Priority="@context.Item.Priority" />
                </CellTemplate>
            </TemplateColumn>
            <PropertyColumn Property="x => x.AssignedAgentName" Title="Agent" />
            <PropertyColumn Property="x => x.CreatedAt" Title="Created" Format="yyyy-MM-dd HH:mm" />
        </Columns>
    </MudDataGrid>
}
```

**Code-behind** (`Pages/Tickets/TicketList.razor.cs`):
```csharp
namespace SupportHub.Web.Pages.Tickets;

public partial class TicketList
{
    [Inject] private ITicketService TicketService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private MudDataGrid<TicketSummaryDto>? _dataGrid;
    private string? _searchTerm;
    private bool _loading;
    private string? _error;

    private async Task<GridData<TicketSummaryDto>> LoadTicketsAsync(GridState<TicketSummaryDto> state)
    {
        var filter = new TicketFilterRequest
        {
            Page = state.Page + 1,  // MudDataGrid is 0-indexed
            PageSize = state.PageSize,
            SearchTerm = _searchTerm
        };

        var result = await TicketService.GetTicketsAsync(filter);

        if (!result.IsSuccess)
        {
            Snackbar.Add(result.Error ?? "Failed to load tickets.", Severity.Error);
            return new GridData<TicketSummaryDto>([], 0);
        }

        return new GridData<TicketSummaryDto>(result.Value!.Items, result.Value.TotalCount);
    }

    private async Task OnSearchChanged(string value)
    {
        _searchTerm = value;
        if (_dataGrid is not null)
            await _dataGrid.ReloadServerData();
    }
}
```

### Component Pattern
```razor
@* Components/TicketStatusChip.razor *@
<MudChip T="string"
         Color="@GetColor()"
         Size="Size.Small"
         Variant="Variant.Filled">
    @Status
</MudChip>

@code {
    [Parameter, EditorRequired]
    public string Status { get; set; } = string.Empty;

    private Color GetColor() => Status switch
    {
        "New" => Color.Info,
        "Open" => Color.Primary,
        "Pending" => Color.Warning,
        "OnHold" => Color.Default,
        "Resolved" => Color.Success,
        "Closed" => Color.Dark,
        _ => Color.Default
    };
}
```

### Form Pattern
```razor
<MudForm @ref="_form" @bind-IsValid="_isValid">
    <MudTextField T="string"
                  @bind-Value="_model.Subject"
                  Label="Subject"
                  Required="true"
                  RequiredError="Subject is required"
                  MaxLength="500"
                  Counter="500" />

    <MudSelect T="TicketPriority"
               @bind-Value="_model.Priority"
               Label="Priority"
               Required="true">
        @foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            <MudSelectItem Value="priority">@priority</MudSelectItem>
        }
    </MudSelect>

    <MudTextField T="string"
                  @bind-Value="_model.Description"
                  Label="Description"
                  Required="true"
                  Lines="5"
                  MaxLength="10000" />

    <MudButton Variant="Variant.Filled"
               Color="Color.Primary"
               Disabled="@(!_isValid || _submitting)"
               OnClick="HandleSubmitAsync">
        @if (_submitting)
        {
            <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
        }
        Submit
    </MudButton>
</MudForm>
```

### Dialog Pattern
```csharp
// Opening a dialog
private async Task OpenEditDialog(CompanyDto company)
{
    var parameters = new DialogParameters<CompanyEditDialog>
    {
        { x => x.Company, company }
    };

    var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
    var dialog = await DialogService.ShowAsync<CompanyEditDialog>("Edit Company", parameters, options);
    var result = await dialog.Result;

    if (!result!.Canceled)
    {
        await _dataGrid!.ReloadServerData();
        Snackbar.Add("Company updated successfully.", Severity.Success);
    }
}
```

### Layout Pattern
```razor
@* Layout/MainLayout.razor *@
@inherits LayoutComponentBase

<MudThemeProvider @ref="_themeProvider" @bind-IsDarkMode="_isDarkMode" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu"
                       Color="Color.Inherit"
                       Edge="Edge.Start"
                       OnClick="ToggleDrawer" />
        <MudText Typo="Typo.h6">Ralis Support Hub</MudText>
        <MudSpacer />
        <CompanySelector />
        <MudIconButton Icon="@Icons.Material.Filled.DarkMode"
                       Color="Color.Inherit"
                       OnClick="ToggleDarkMode" />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" Elevation="2">
        <MudDrawerHeader>
            <MudText Typo="Typo.h6">Navigation</MudText>
        </MudDrawerHeader>
        <NavMenu />
    </MudDrawer>

    <MudMainContent Class="pa-4">
        @Body
    </MudMainContent>
</MudLayout>
```

### Error Handling Pattern
```csharp
private async Task HandleSubmitAsync()
{
    _submitting = true;
    _error = null;

    try
    {
        var result = await TicketService.CreateTicketAsync(_model);

        if (result.IsSuccess)
        {
            Snackbar.Add("Ticket created successfully.", Severity.Success);
            Navigation.NavigateTo($"/tickets/{result.Value!.Id}");
        }
        else
        {
            _error = result.Error;
            Snackbar.Add(result.Error ?? "An error occurred.", Severity.Error);
        }
    }
    catch (Exception ex)
    {
        _error = "An unexpected error occurred. Please try again.";
        Snackbar.Add(_error, Severity.Error);
        Logger.LogError(ex, "Error creating ticket");
    }
    finally
    {
        _submitting = false;
    }
}
```

## Page Inventory by Phase

### Phase 1
- `/` — Dashboard (placeholder, replaced in Phase 6)
- `/admin/companies` — Company list + CRUD
- `/admin/companies/{id}` — Company detail with divisions
- `/admin/users` — User list
- `/admin/users/{id}` — User detail with role assignments
- Layout: MainLayout, NavMenu

### Phase 2
- `/tickets/create` — Structured web form intake
- `/tickets` — Ticket list with filtering/sorting
- `/tickets/{id}` — Ticket detail with conversation view
- `/admin/canned-responses` — Canned response CRUD
- Components: TicketStatusChip, TicketPriorityChip, ConversationTimeline, FileUploadComponent, TagInput

### Phase 3
- `/admin/email-configurations` — Email config CRUD
- `/admin/email-logs` — Email processing logs

### Phase 4
- `/admin/queues` — Queue management
- `/admin/routing-rules` — Routing rules with drag-drop reorder

### Phase 5
- `/admin/sla-policies` — SLA policy CRUD
- `/admin/sla-breaches` — SLA breach list
- Components: SlaIndicator, CsatSurvey, CsatSummaryWidget

### Phase 6
- `/` — Dashboard with metrics and charts (replace placeholder)
- `/reports/audit` — Audit report with export
- `/reports/tickets` — Ticket report with export
- `/kb` — Knowledge base list/search
- `/kb/{slug}` — Article view
- `/admin/kb` — KB article admin

## Common Anti-Patterns to AVOID

1. **Calling services in OnInitializedAsync without loading state** — Always show a loading indicator while data loads.
2. **Missing error handling** — Every service call result must check `IsSuccess` and handle failure.
3. **Inline code in .razor files** — Use code-behind (.razor.cs) for anything beyond trivial logic.
4. **Not using MudBlazor components** — Use MudTextField, MudSelect, MudDataGrid etc. Don't use raw HTML form elements.
5. **Hardcoded strings for navigation** — Use constants or `NavigationManager` patterns.
6. **Missing [Authorize] attribute** — Every page must have `@attribute [Authorize]` (or a more specific policy).
7. **Missing [EditorRequired] on required parameters** — Mark required component parameters.
8. **Not using Snackbar for feedback** — Use `ISnackbar` for success/error messages, not inline text.
9. **Forgetting StateHasChanged** — If updating UI from a callback/SignalR, call `await InvokeAsync(StateHasChanged)`.
10. **Not disposing event subscriptions** — Implement `IDisposable` if subscribing to events.

## Completion Checklist (per wave)
- [ ] All pages have `@page` directive with correct route
- [ ] All pages have `@attribute [Authorize]` (with policy if needed)
- [ ] All pages have `<PageTitle>` set
- [ ] Loading states shown during async operations
- [ ] Error states displayed with MudAlert
- [ ] Forms validate required fields before submission
- [ ] Submit buttons disabled during submission with loading spinner
- [ ] Service call results checked for IsSuccess
- [ ] Snackbar notifications for success/failure
- [ ] Code-behind used for non-trivial logic
- [ ] MudBlazor components used consistently (no raw HTML forms)
- [ ] NavMenu updated with new page links
- [ ] `dotnet build` succeeds with zero errors and zero warnings
