using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
/// The first-run guided setup: pick a level, review the reversible privacy/cleanup tweaks it
/// selects, choose which preinstalled bloat to remove, review a plain-language summary, then apply.
/// It is a guided front-end over the same engine the Cleanup and Apps pages use — nothing is applied
/// until the final step, and policy changes stay reversible from Restore points.
/// </summary>
public sealed partial class WizardPage : Page
{
    private const int ReviewStep = 3;
    private const int DoneStep = 4;

    private readonly ObservableCollection<PolicyViewModel> _policies = [];
    private readonly ObservableCollection<AppRowViewModel> _bloat = [];
    private int _step;
    private bool _loaded;
    private bool _applying;

    /// <summary>Initializes the page.</summary>
    public WizardPage()
    {
        this.InitializeComponent();
        this.PolicyList.ItemsSource = this._policies;
        this.BloatList.ItemsSource = this._bloat;
        this.ShowStep();
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!this._loaded)
        {
            _ = this.LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        this.SetBusy(true);
        try
        {
            (PolicyListResult policies, AppListResult apps, AppListResult programs) = await SovereignClient.RunAsync(async c =>
            {
                PolicyListResult p = await c.ListPoliciesAsync().ConfigureAwait(false);
                AppListResult a = await c.ListAppsAsync().ConfigureAwait(false);
                AppListResult pr = await c.ListProgramsAsync().ConfigureAwait(false);
                return (p, a, pr);
            }).ConfigureAwait(true);

            this._policies.Clear();
            foreach (PolicyInfo info in policies.Policies.OrderBy(p => p.Level).ThenBy(p => p.Category, StringComparer.Ordinal))
            {
                this._policies.Add(new PolicyViewModel(info));
            }

            this._bloat.Clear();
            foreach (AppInfo info in apps.Apps.Concat(programs.Apps).Where(a => a.Recommended && a.Removable))
            {
                var vm = new AppRowViewModel(info);
                this._bloat.Add(vm);
                _ = vm.LoadIconAsync();
            }

            this._loaded = true;
            this.ApplyLevelSelection();
        }
        catch (IpcException)
        {
            this.Step1Subtitle.Text = "Couldn't reach the Sovereign service. Make sure it's running, then reopen setup.";
        }
        finally
        {
            this.SetBusy(false);
        }
    }

    private void OnLevelChecked(object sender, RoutedEventArgs e)
    {
        if (this._loaded)
        {
            this.ApplyLevelSelection();
        }
    }

    private PolicyLevel SelectedLevel() =>
        this.LevelPro.IsChecked == true ? PolicyLevel.Pro
        : this.LevelLite.IsChecked == true ? PolicyLevel.Lite
        : PolicyLevel.Normal;

    private void ApplyLevelSelection()
    {
        PolicyLevel level = this.SelectedLevel();
        foreach (PolicyViewModel vm in this._policies)
        {
            vm.IsSelected = vm.Level <= level;
        }

        int count = this._policies.Count(p => p.IsSelected);
        this.Step1Subtitle.Text = $"{count} reversible tweaks are pre-selected for your level. Untick anything you want to keep.";
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (this._step > 0 && !this._applying)
        {
            this._step--;
            this.ShowStep();
        }
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        if (this._applying)
        {
            return;
        }

        if (this._step == ReviewStep)
        {
            await this.ApplyAsync().ConfigureAwait(true);
            return;
        }

        if (this._step == DoneStep)
        {
            App.Shell?.NavigateToDashboard();
            return;
        }

        this._step++;
        if (this._step == ReviewStep)
        {
            this.BuildReview();
        }

        this.ShowStep();
    }

    private void OnSkip(object sender, RoutedEventArgs e) => App.Shell?.NavigateToDashboard();

    private void OnOpenRestore(object sender, RoutedEventArgs e) => App.Shell?.NavigateToRestore();

    private void OnOpenCleanup(object sender, RoutedEventArgs e) => App.Shell?.NavigateToCleanup();

    private void BuildReview()
    {
        int tweaks = this._policies.Count(p => p.IsSelected);
        List<AppRowViewModel> apps = this._bloat.Where(a => a.IsSelected && a.CanSelect).ToList();
        int storeApps = apps.Count(a => !a.IsProgram);
        int programs = apps.Count(a => a.IsProgram);

        this.ReviewSummary.Text = string.Format(
            CultureInfo.CurrentCulture,
            "You're about to apply {0} privacy/cleanup tweak(s), remove {1} Store app(s), and uninstall {2} program(s).",
            tweaks,
            storeApps,
            programs);

        this.ReviewDetail.Children.Clear();
        AddReviewLine($"• {tweaks} reversible tweak(s) — undo anytime from Restore points");
        if (storeApps > 0)
        {
            AddReviewLine($"• {storeApps} Store app(s) — reinstallable from the Microsoft Store");
        }

        if (programs > 0)
        {
            AddReviewLine($"• {programs} installed program(s) — removed via their uninstaller, not reversible here");
        }
    }

    private void AddReviewLine(string text) =>
        this.ReviewDetail.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });

    private async Task ApplyAsync()
    {
        List<PolicyViewModel> policies = this._policies.Where(p => p.IsSelected).ToList();
        List<AppRowViewModel> apps = this._bloat.Where(a => a.IsSelected && a.CanSelect).ToList();

        this._applying = true;
        this.SetBusy(true);
        this.ApplyProgress.Visibility = Visibility.Visible;
        this.ApplyStatus.Visibility = Visibility.Visible;
        this.ApplyProgress.Maximum = Math.Max(1, policies.Count + apps.Count);
        this.ApplyProgress.Value = 0;

        int tweaksApplied = 0;
        int appsRemoved = 0;
        var failures = new List<string>();

        try
        {
            await SovereignClient.RunAsync(async client =>
            {
                foreach (PolicyViewModel vm in policies)
                {
                    this.SetApplyStatus($"Applying: {vm.Title}");
                    PolicyRunResult run = await client.ApplyPolicyAsync(vm.Id).ConfigureAwait(true);
                    if (run.State is PolicyResultState.Applied or PolicyResultState.Compliant)
                    {
                        tweaksApplied++;
                    }
                    else
                    {
                        failures.Add($"{vm.Title}: {run.State}");
                    }

                    this.StepProgress();
                }

                foreach (AppRowViewModel vm in apps)
                {
                    this.SetApplyStatus($"Removing: {vm.DisplayName}");
                    AppActionResult result = vm.IsProgram
                        ? await client.RemoveProgramAsync(vm.PackageFullName).ConfigureAwait(true)
                        : await client.RemoveAppAsync(vm.PackageFullName).ConfigureAwait(true);
                    if (result.Success)
                    {
                        appsRemoved++;
                        vm.IsRemoved = true;
                        vm.IsSelected = false;
                    }
                    else
                    {
                        failures.Add($"{vm.DisplayName}: {result.Detail}");
                    }

                    this.StepProgress();
                }
            }).ConfigureAwait(true);
        }
        catch (IpcException ex)
        {
            failures.Add($"Service connection lost: {ex.Message}");
        }

        this._applying = false;
        this.SetBusy(false);
        this.ApplyProgress.Visibility = Visibility.Collapsed;
        this.ApplyStatus.Visibility = Visibility.Collapsed;

        this.DoneSummary.Text = failures.Count == 0
            ? $"Applied {tweaksApplied} tweak(s) and removed {appsRemoved} app(s)/program(s). Your privacy tweaks can be undone anytime from Restore points."
            : $"Applied {tweaksApplied} tweak(s) and removed {appsRemoved} app(s)/program(s). {failures.Count} item(s) need attention:\n• " + string.Join("\n• ", failures.Take(8));

        this._step = DoneStep;
        this.ShowStep();
    }

    private void StepProgress() => this.ApplyProgress.Value += 1;

    private void SetApplyStatus(string text) => this.ApplyStatus.Text = text;

    private void ShowStep()
    {
        this.Step0.Visibility = this._step == 0 ? Visibility.Visible : Visibility.Collapsed;
        this.Step1.Visibility = this._step == 1 ? Visibility.Visible : Visibility.Collapsed;
        this.Step2.Visibility = this._step == 2 ? Visibility.Visible : Visibility.Collapsed;
        this.Step3.Visibility = this._step == ReviewStep ? Visibility.Visible : Visibility.Collapsed;
        this.Step4.Visibility = this._step == DoneStep ? Visibility.Visible : Visibility.Collapsed;

        this.HighlightRail();

        this.BackButton.Visibility = this._step is > 0 and < DoneStep ? Visibility.Visible : Visibility.Collapsed;
        this.SkipButton.Visibility = this._step < DoneStep ? Visibility.Visible : Visibility.Collapsed;
        this.NextButton.Content = this._step == ReviewStep ? "Apply" : this._step == DoneStep ? "Finish" : "Next";
    }

    private void HighlightRail()
    {
        TextBlock[] rails = [this.Rail0, this.Rail1, this.Rail2, this.Rail3, this.Rail4];
        for (int i = 0; i < rails.Length; i++)
        {
            bool current = i == this._step;
            rails[i].FontWeight = current ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            rails[i].Opacity = current ? 1.0 : i < this._step ? 0.7 : 0.45;
        }
    }

    private void SetBusy(bool busy)
    {
        this.Busy.IsActive = busy;
        this.NextButton.IsEnabled = !busy;
        this.BackButton.IsEnabled = !busy;
    }
}
