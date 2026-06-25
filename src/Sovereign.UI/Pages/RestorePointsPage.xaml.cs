using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sovereign.Contracts;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;
using Sovereign.UI.Services;

namespace Sovereign.UI.Pages;

/// <summary>A single restore point row.</summary>
/// <param name="PolicyId">The policy this restore point belongs to.</param>
/// <param name="CreatedText">Formatted local capture time.</param>
public sealed record RestorePointRow(string PolicyId, string CreatedText);

/// <summary>
/// Lists captured restore points and lets the user revert any policy to its captured original state.
/// </summary>
public sealed partial class RestorePointsPage : Page
{
    private readonly ObservableCollection<RestorePointRow> _points = [];

    /// <summary>Initializes the page.</summary>
    public RestorePointsPage()
    {
        this.InitializeComponent();
        this.PointsList.ItemsSource = this._points;
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = this.RefreshAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await this.RefreshAsync().ConfigureAwait(true);

    private async Task RefreshAsync()
    {
        try
        {
            RestorePointListResult result = await SovereignClient.RunAsync(c => c.ListRestorePointsAsync()).ConfigureAwait(true);
            this._points.Clear();
            foreach (RestorePointInfo point in result.RestorePoints)
            {
                this._points.Add(new RestorePointRow(
                    point.PolicyId,
                    point.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)));
            }

            this.EmptyText.Visibility = this._points.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (IpcException ex)
        {
            this.ShowResult(InfoBarSeverity.Error, "Service offline", ex.Message);
        }
    }

    private async void OnRevert(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string policyId })
        {
            return;
        }

        try
        {
            PolicyRunResult run = await SovereignClient.RunAsync(c => c.RollbackPolicyAsync(policyId)).ConfigureAwait(true);
            if (run.State == PolicyResultState.Applied)
            {
                this.ShowResult(InfoBarSeverity.Success, "Reverted", $"{policyId} was restored to its captured original state.");
            }
            else
            {
                this.ShowResult(InfoBarSeverity.Warning, "Revert reported an issue", $"{policyId}: {run.State}{(run.FailureDetail is null ? string.Empty : $" ({run.FailureDetail})")}");
            }
        }
        catch (IpcException ex)
        {
            this.ShowResult(InfoBarSeverity.Error, "Service offline", ex.Message);
        }
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        this.ResultBar.Severity = severity;
        this.ResultBar.Title = title;
        this.ResultBar.Message = message;
        this.ResultBar.IsOpen = true;
    }
}
