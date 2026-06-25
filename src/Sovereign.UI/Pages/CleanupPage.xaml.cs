using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sovereign.Contracts;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;
using Sovereign.UI.Services;
using Sovereign.UI.ViewModels;

namespace Sovereign.UI.Pages;

/// <summary>
/// The cleanup and hardening page: choose a preset level (Lite/Normal/Pro), review the policies it
/// selects, and apply them. Applying calls the service one policy at a time; each apply captures a
/// restore point first, so everything here is reversible from the Restore points page.
/// </summary>
public sealed partial class CleanupPage : Page
{
    private readonly ObservableCollection<PolicyViewModel> _policies = [];
    private bool _loaded;

    /// <summary>Initializes the page.</summary>
    public CleanupPage()
    {
        this.InitializeComponent();
        this.PolicyList.ItemsSource = this._policies;
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = this.LoadAsync();
    }

    private async Task LoadAsync()
    {
        this.SetBusy(true);
        try
        {
            PolicyListResult list = await SovereignClient.RunAsync(c => c.ListPoliciesAsync()).ConfigureAwait(true);

            foreach (PolicyViewModel existing in this._policies)
            {
                existing.PropertyChanged -= this.OnPolicyChanged;
            }

            this._policies.Clear();
            foreach (PolicyInfo info in list.Policies.OrderBy(p => p.Level).ThenBy(p => p.Category, StringComparer.Ordinal))
            {
                var vm = new PolicyViewModel(info);
                vm.PropertyChanged += this.OnPolicyChanged;
                this._policies.Add(vm);
            }

            this._loaded = true;
            this.ApplyLevelSelection();
            await this.DetectAllAsync().ConfigureAwait(true);
        }
        catch (IpcException ex)
        {
            this.ShowResult(InfoBarSeverity.Error, "Service offline", $"{ex.Message} Start the Sovereign service and re-scan.");
        }
        finally
        {
            this.SetBusy(false);
        }
    }

    private async Task DetectAllAsync()
    {
        try
        {
            // Detect off-thread, collecting results, then apply state on the UI thread. Touching the
            // bound view models from a background thread throws a wrong-thread COM exception, which is
            // what left every tile stuck on "Checking...".
            var results = new List<(PolicyViewModel Vm, PolicyResultState State)>();
            await SovereignClient.RunAsync(async client =>
            {
                foreach (PolicyViewModel vm in this._policies)
                {
                    PolicyDetectResult detect = await client.DetectPolicyAsync(vm.Id).ConfigureAwait(false);
                    results.Add((vm, detect.State));
                }
            }).ConfigureAwait(true);

            foreach ((PolicyViewModel vm, PolicyResultState state) in results)
            {
                vm.ApplyState(state);
            }
        }
        catch (IpcException)
        {
            // Leave the last known badges in place; the result bar already reports connection issues.
        }
    }

    private void OnLevelChecked(object sender, RoutedEventArgs e)
    {
        if (this._loaded)
        {
            this.ApplyLevelSelection();
        }
    }

    private void ApplyLevelSelection()
    {
        PolicyLevel level = this.SelectedLevel();
        foreach (PolicyViewModel vm in this._policies)
        {
            vm.IsSelected = vm.Level <= level;
        }

        this.UpdateSelectionText();
    }

    private PolicyLevel SelectedLevel()
    {
        if (this.ProRadio.IsChecked == true)
        {
            return PolicyLevel.Pro;
        }

        return this.LiteRadio.IsChecked == true ? PolicyLevel.Lite : PolicyLevel.Normal;
    }

    private void OnPolicyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PolicyViewModel.IsSelected))
        {
            this.UpdateSelectionText();
        }
    }

    private void UpdateSelectionText()
    {
        int count = this._policies.Count(p => p.IsSelected);
        this.SelectionText.Text = $"{count} selected";
    }

    private async void OnRescan(object sender, RoutedEventArgs e)
    {
        this.SetBusy(true);
        try
        {
            await this.DetectAllAsync().ConfigureAwait(true);
        }
        finally
        {
            this.SetBusy(false);
        }
    }

    private async void OnApplySelected(object sender, RoutedEventArgs e)
    {
        List<PolicyViewModel> selected = this._policies.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            this.ShowResult(InfoBarSeverity.Informational, "Nothing selected", "Pick at least one tweak to apply.");
            return;
        }

        this.SetBusy(true);
        int succeeded = 0;
        int changed = 0;
        var failures = new List<string>();
        var states = new List<(PolicyViewModel Vm, PolicyResultState State)>();

        try
        {
            await SovereignClient.RunAsync(async client =>
            {
                foreach (PolicyViewModel vm in selected)
                {
                    PolicyRunResult run = await client.ApplyPolicyAsync(vm.Id).ConfigureAwait(false);
                    if (run.State is PolicyResultState.Applied or PolicyResultState.Compliant)
                    {
                        succeeded++;
                        changed += run.Changes.Count;
                        states.Add((vm, PolicyResultState.Compliant));
                    }
                    else
                    {
                        failures.Add($"{vm.Title}: {run.State}{(run.FailureDetail is null ? string.Empty : $" ({run.FailureDetail})")}");
                        states.Add((vm, run.State));
                    }
                }
            }).ConfigureAwait(true);
        }
        catch (IpcException ex)
        {
            this.ShowResult(InfoBarSeverity.Error, "Service offline", ex.Message);
            this.SetBusy(false);
            return;
        }

        foreach ((PolicyViewModel vm, PolicyResultState state) in states)
        {
            vm.ApplyState(state);
        }

        if (failures.Count == 0)
        {
            this.ShowResult(
                InfoBarSeverity.Success,
                "Applied",
                $"Applied {succeeded} tweak(s), {changed} change(s). Undo anytime from Restore points.");
        }
        else
        {
            this.ShowResult(
                InfoBarSeverity.Warning,
                $"Applied {succeeded}, {failures.Count} need attention",
                string.Join(Environment.NewLine, failures));
        }

        this.SetBusy(false);
    }

    private void SetBusy(bool busy)
    {
        this.Busy.IsActive = busy;
        this.ApplyButton.IsEnabled = !busy;
        this.RescanButton.IsEnabled = !busy;
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        this.ResultBar.Severity = severity;
        this.ResultBar.Title = title;
        this.ResultBar.Message = message;
        this.ResultBar.IsOpen = true;
    }
}
