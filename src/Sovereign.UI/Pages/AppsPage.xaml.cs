using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;
using Sovereign.UI.Services;
using Sovereign.UI.ViewModels;

namespace Sovereign.UI.Pages;

/// <summary>
/// Enumerates installed Appx/MSIX packages for all users, groups them by category with icons and
/// descriptions, pre-selects known bloat, and removes the chosen apps via the service after an
/// explicit confirmation. Protected/system apps are read-only. Removal is audited and surfaced
/// honestly as non-reversible.
/// </summary>
public sealed partial class AppsPage : Page
{
    private enum AppFilter
    {
        Recommended,
        All,
        System,
    }

    private readonly List<AppRowViewModel> _all = [];
    private AppFilter _filter = AppFilter.Recommended;
    private string _search = string.Empty;
    private bool _loaded;

    /// <summary>Initializes the page.</summary>
    public AppsPage()
    {
        this.InitializeComponent();
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

    private async void OnRescan(object sender, RoutedEventArgs e) => await this.LoadAsync().ConfigureAwait(true);

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, this.FilterAll))
        {
            this._filter = AppFilter.All;
        }
        else if (ReferenceEquals(sender, this.FilterSystem))
        {
            this._filter = AppFilter.System;
        }
        else
        {
            this._filter = AppFilter.Recommended;
        }

        this.ApplyFilter();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        this._search = this.SearchBox.Text.Trim();
        this.ApplyFilter();
    }

    private async Task LoadAsync()
    {
        this.SetBusy(true, "Scanning installed apps & programs…");
        try
        {
            (AppListResult apps, AppListResult programs) = await SovereignClient.RunAsync(async c =>
            {
                AppListResult a = await c.ListAppsAsync().ConfigureAwait(false);
                AppListResult p = await c.ListProgramsAsync().ConfigureAwait(false);
                return (a, p);
            }).ConfigureAwait(true);

            this._all.Clear();
            foreach (AppInfo info in apps.Apps.Concat(programs.Apps))
            {
                var vm = new AppRowViewModel(info);
                this._all.Add(vm);
                _ = vm.LoadIconAsync();
            }

            this._loaded = true;
            this.ApplyFilter();
            this.UpdateSummary();
        }
        catch (IpcException ex)
        {
            this.ShowResult(InfoBarSeverity.Error, "Service offline", ex.Message);
            this.SummaryText.Text = "Could not reach the Sovereign service.";
        }
        finally
        {
            this.SetBusy(false, null);
        }
    }

    private void ApplyFilter()
    {
        // The "Recommended" radio's Checked event fires during InitializeComponent (before the list and
        // data exist). Stay inert until the first successful load wires everything up.
        if (!this._loaded)
        {
            return;
        }

        IEnumerable<AppRowViewModel> filtered = this._filter switch
        {
            AppFilter.Recommended => this._all.Where(a => a.Recommended && a.Removable),
            AppFilter.System => this._all.Where(a => a.IsSystem),
            _ => this._all.Where(a => !a.IsSystem),
        };

        if (this._search.Length > 0)
        {
            filtered = filtered.Where(a =>
                a.DisplayName.Contains(this._search, System.StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains(this._search, System.StringComparison.OrdinalIgnoreCase)
                || a.Publisher.Contains(this._search, System.StringComparison.OrdinalIgnoreCase)
                || a.Category.Contains(this._search, System.StringComparison.OrdinalIgnoreCase));
        }

        List<AppGroup> groups = filtered
            .GroupBy(a => a.Category)
            .Select(g => new AppGroup(g.Key, g.OrderBy(a => a.DisplayName, System.StringComparer.OrdinalIgnoreCase)))
            .OrderByDescending(g => g.Any(a => a.Recommended))
            .ThenBy(g => g.Key, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        this.AppsCvs.Source = groups;
        this.AppsList.ItemsSource = this.AppsCvs.View;
    }

    private void UpdateSummary()
    {
        int total = this._all.Count;
        int recommended = this._all.Count(a => a.Recommended && a.Removable);
        int programs = this._all.Count(a => a.IsProgram);
        this.SummaryText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "{0} items · {1} programs · {2} recommended for removal",
            total,
            programs,
            recommended);
    }

    private async void OnRemoveSelected(object sender, RoutedEventArgs e)
    {
        List<AppRowViewModel> selected = this._all.Where(a => a.IsSelected && a.CanSelect).ToList();
        if (selected.Count == 0)
        {
            this.ShowResult(InfoBarSeverity.Informational, "Nothing selected", "Tick the apps you want to remove first.");
            return;
        }

        ContentDialogResult confirm = await this.ConfirmAsync(selected).ConfigureAwait(true);
        if (confirm != ContentDialogResult.Primary)
        {
            return;
        }

        this.SetBusy(true, $"Removing {selected.Count} app(s)…");
        int removed = 0;
        var failures = new List<string>();
        var results = new List<(AppRowViewModel Vm, bool Success)>();

        try
        {
            await SovereignClient.RunAsync(async client =>
            {
                foreach (AppRowViewModel vm in selected)
                {
                    AppActionResult result = vm.IsProgram
                        ? await client.RemoveProgramAsync(vm.PackageFullName).ConfigureAwait(false)
                        : await client.RemoveAppAsync(vm.PackageFullName).ConfigureAwait(false);
                    if (result.Success)
                    {
                        removed++;
                        results.Add((vm, true));
                    }
                    else
                    {
                        failures.Add($"{vm.DisplayName}: {result.Detail}");
                        results.Add((vm, false));
                    }
                }
            }).ConfigureAwait(true);
        }
        catch (IpcException ex)
        {
            this.ShowResult(InfoBarSeverity.Error, "Service offline", ex.Message);
            this.SetBusy(false, null);
            return;
        }

        foreach ((AppRowViewModel vm, bool success) in results)
        {
            if (success)
            {
                vm.IsSelected = false;
                vm.IsRemoved = true;
            }
        }

        this.SetBusy(false, null);
        this.UpdateSummary();

        if (failures.Count == 0)
        {
            this.ShowResult(InfoBarSeverity.Success, "Done", $"Removed {removed} app(s) for all users.");
        }
        else
        {
            this.ShowResult(
                InfoBarSeverity.Warning,
                $"Removed {removed}, {failures.Count} failed",
                string.Join("\n", failures));
        }
    }

    private async Task<ContentDialogResult> ConfirmAsync(List<AppRowViewModel> selected)
    {
        IEnumerable<string> names = selected.Take(12).Select(a => "• " + a.DisplayName);
        string body = string.Join("\n", names);
        if (selected.Count > 12)
        {
            body += $"\n… and {selected.Count - 12} more";
        }

        int programs = selected.Count(a => a.IsProgram);
        int storeApps = selected.Count - programs;
        var notes = new List<string>();
        if (storeApps > 0)
        {
            notes.Add("Store apps are uninstalled for every user and deprovisioned so they don't come back for new accounts. You can reinstall them from the Microsoft Store later.");
        }

        if (programs > 0)
        {
            notes.Add("Installed programs are removed by running their own uninstaller. This can take a minute each and cannot be undone from here — you'd reinstall from the vendor if you change your mind.");
        }

        var dialog = new ContentDialog
        {
            Title = $"Remove {selected.Count} item(s)?",
            Content = string.Join("\n\n", notes) + "\n\n" + body,
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        return await dialog.ShowAsync().AsTask().ConfigureAwait(true);
    }

    private void SetBusy(bool busy, string? message)
    {
        this.Busy.IsActive = busy;
        this.RemoveButton.IsEnabled = !busy;
        if (message is not null)
        {
            this.SummaryText.Text = message;
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
