namespace SupportHub.Web.Components.Pages.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

[Authorize(Policy = "Admin")]
public partial class RoutingRules
{
    [Inject] private IRoutingRuleService RoutingRuleService { get; set; } = null!;
    [Inject] private IQueueService QueueService { get; set; } = null!;
    [Inject] private ICompanyService CompanyService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    private List<RoutingRuleDto> _rules = [];
    private List<QueueDto> _queues = [];
    private List<CompanyDto> _companies = [];
    private Guid? _selectedCompanyId;
    private bool _loading = true;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await LoadCompaniesAsync();
        if (_companies.Count > 0)
        {
            _selectedCompanyId = _companies[0].Id;
            await LoadDataAsync();
        }
        _loading = false;
    }

    private async Task LoadCompaniesAsync()
    {
        var result = await CompanyService.GetCompaniesAsync(1, 100);
        if (result.IsSuccess)
            _companies = result.Value!.Items.ToList();
    }

    private async Task LoadDataAsync()
    {
        if (!_selectedCompanyId.HasValue) return;
        _loading = true;
        _error = null;

        var rulesResult = await RoutingRuleService.GetRulesAsync(_selectedCompanyId.Value);
        if (rulesResult.IsSuccess)
            _rules = rulesResult.Value!.ToList();
        else
            _error = rulesResult.Error;

        var queuesResult = await QueueService.GetQueuesAsync(_selectedCompanyId.Value, 1, 100);
        if (queuesResult.IsSuccess)
            _queues = queuesResult.Value!.Items.ToList();

        _loading = false;
    }

    private async Task OnCompanyChanged(Guid companyId)
    {
        _selectedCompanyId = companyId;
        await LoadDataAsync();
    }

    private async Task OpenCreateDialogAsync()
    {
        var parameters = new DialogParameters<RoutingRuleFormDialog>
        {
            { x => x.CompanyId, _selectedCompanyId ?? Guid.Empty },
            { x => x.AvailableQueues, _queues }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<RoutingRuleFormDialog>("Add Routing Rule", parameters, options);
        var result = await dialog.Result;
        if (!result!.Canceled)
        {
            await LoadDataAsync();
            Snackbar.Add("Routing rule created.", Severity.Success);
        }
    }

    private async Task OpenEditDialogAsync(RoutingRuleDto rule)
    {
        var parameters = new DialogParameters<RoutingRuleFormDialog>
        {
            { x => x.Rule, rule },
            { x => x.CompanyId, rule.CompanyId },
            { x => x.AvailableQueues, _queues }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<RoutingRuleFormDialog>("Edit Routing Rule", parameters, options);
        var result = await dialog.Result;
        if (!result!.Canceled)
        {
            await LoadDataAsync();
            Snackbar.Add("Routing rule updated.", Severity.Success);
        }
    }

    private async Task DeleteRuleAsync(RoutingRuleDto rule)
    {
        var confirm = await DialogService.ShowMessageBox(
            "Delete Rule",
            $"Are you sure you want to delete the rule '{rule.Name}'?",
            "Delete", "Cancel");
        if (confirm != true) return;

        var result = await RoutingRuleService.DeleteRuleAsync(rule.Id);
        if (result.IsSuccess)
        {
            _rules.Remove(rule);
            Snackbar.Add("Rule deleted.", Severity.Success);
        }
        else
        {
            Snackbar.Add(result.Error ?? "Failed to delete rule.", Severity.Error);
        }
    }

    private async Task MoveUpAsync(RoutingRuleDto rule)
    {
        var index = _rules.IndexOf(rule);
        if (index <= 0) return;
        _rules.RemoveAt(index);
        _rules.Insert(index - 1, rule);
        await SaveOrderAsync();
    }

    private async Task MoveDownAsync(RoutingRuleDto rule)
    {
        var index = _rules.IndexOf(rule);
        if (index >= _rules.Count - 1) return;
        _rules.RemoveAt(index);
        _rules.Insert(index + 1, rule);
        await SaveOrderAsync();
    }

    private async Task SaveOrderAsync()
    {
        if (!_selectedCompanyId.HasValue) return;
        var request = new ReorderRoutingRulesRequest(_rules.Select(r => r.Id).ToList());
        var result = await RoutingRuleService.ReorderRulesAsync(_selectedCompanyId.Value, request);
        if (result.IsSuccess)
        {
            await LoadDataAsync();
            Snackbar.Add("Order saved.", Severity.Success);
        }
        else
        {
            Snackbar.Add(result.Error ?? "Failed to reorder rules.", Severity.Error);
        }
    }
}
