namespace SupportHub.Web.Components.Pages.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using SupportHub.Application.DTOs;
using SupportHub.Application.Interfaces;

[Authorize(Policy = "Admin")]
public partial class Queues
{
    [Inject] private IQueueService QueueService { get; set; } = null!;
    [Inject] private ICompanyService CompanyService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

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
            await LoadQueuesAsync();
        }
        _loading = false;
    }

    private async Task LoadCompaniesAsync()
    {
        var result = await CompanyService.GetCompaniesAsync(1, 100);
        if (result.IsSuccess)
            _companies = result.Value!.Items.ToList();
    }

    private async Task LoadQueuesAsync()
    {
        if (!_selectedCompanyId.HasValue) return;
        _loading = true;
        var result = await QueueService.GetQueuesAsync(_selectedCompanyId.Value, 1, 100);
        if (result.IsSuccess)
            _queues = result.Value!.Items.ToList();
        else
            _error = result.Error;
        _loading = false;
    }

    private async Task OnCompanyChanged(Guid companyId)
    {
        _selectedCompanyId = companyId;
        await LoadQueuesAsync();
    }

    private async Task OpenCreateDialogAsync()
    {
        var parameters = new DialogParameters<QueueFormDialog>
        {
            { x => x.CompanyId, _selectedCompanyId ?? Guid.Empty },
            { x => x.Companies, _companies }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<QueueFormDialog>("Add Queue", parameters, options);
        var result = await dialog.Result;
        if (!result!.Canceled)
        {
            await LoadQueuesAsync();
            Snackbar.Add("Queue created successfully.", Severity.Success);
        }
    }

    private async Task OpenEditDialogAsync(QueueDto queue)
    {
        var parameters = new DialogParameters<QueueFormDialog>
        {
            { x => x.Queue, queue },
            { x => x.CompanyId, queue.CompanyId },
            { x => x.Companies, _companies }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<QueueFormDialog>("Edit Queue", parameters, options);
        var result = await dialog.Result;
        if (!result!.Canceled)
        {
            await LoadQueuesAsync();
            Snackbar.Add("Queue updated successfully.", Severity.Success);
        }
    }

    private async Task DeleteQueueAsync(QueueDto queue)
    {
        var confirm = await DialogService.ShowMessageBox(
            "Delete Queue",
            $"Are you sure you want to delete the queue '{queue.Name}'?",
            "Delete", "Cancel");
        if (confirm != true) return;

        var result = await QueueService.DeleteQueueAsync(queue.Id);
        if (result.IsSuccess)
        {
            _queues.Remove(queue);
            Snackbar.Add("Queue deleted.", Severity.Success);
        }
        else
        {
            Snackbar.Add(result.Error ?? "Failed to delete queue.", Severity.Error);
        }
    }
}
