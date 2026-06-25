using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Sovereign.Ipc;
using Sovereign.UI.Pages;
using Sovereign.UI.Services;
using Windows.UI;

namespace Sovereign.UI;

/// <summary>
/// The main shell window: a Fluent <see cref="NavigationView"/> hosting the dashboard, cleanup,
/// activity, and restore-point pages. It owns the live connection indicator and degrades to a clear
/// offline state when the service is unreachable (agent_start.md section 9).
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initializes the window, applies the Mica backdrop, and shows the dashboard.</summary>
    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "Sovereign";
        this.SystemBackdrop = new MicaBackdrop();
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(this.AppTitleBar);
        this.TrySetIcon();
        this.ContentFrame.Navigate(typeof(WizardPage));
        _ = this.UpdateConnectionAsync();
    }

    private void TrySetIcon()
    {
        try
        {
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "sovereign.ico");
            if (System.IO.File.Exists(iconPath))
            {
                this.AppWindow.SetIcon(iconPath);
            }
        }
        catch (Exception)
        {
            // A missing or unreadable icon is non-fatal; the app still runs.
        }
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        Type page = tag switch
        {
            "getstarted" => typeof(WizardPage),
            "cleanup" => typeof(CleanupPage),
            "apps" => typeof(AppsPage),
            "events" => typeof(EventsPage),
            "restore" => typeof(RestorePointsPage),
            _ => typeof(DashboardPage),
        };

        if (this.ContentFrame.CurrentSourcePageType != page)
        {
            this.ContentFrame.Navigate(page);
        }

        _ = this.UpdateConnectionAsync();
    }

    /// <summary>Navigates the content frame to the cleanup page (used by dashboard quick actions).</summary>
    public void NavigateToCleanup() => this.SelectNav("cleanup");

    /// <summary>Navigates to the dashboard (used to leave the setup wizard).</summary>
    public void NavigateToDashboard() => this.SelectNav("dashboard");

    /// <summary>Navigates to the restore points page.</summary>
    public void NavigateToRestore() => this.SelectNav("restore");

    private void SelectNav(string tag)
    {
        foreach (object menuItem in this.Nav.MenuItems)
        {
            if (menuItem is NavigationViewItem item && (item.Tag as string) == tag)
            {
                this.Nav.SelectedItem = item;
                return;
            }
        }
    }

    private async Task UpdateConnectionAsync()
    {
        try
        {
            string version = await SovereignClient.RunAsync(c => c.GetVersionAsync()).ConfigureAwait(true);
            this.SetStatus($"Connected \u2022 {version}", connected: true);
        }
        catch (IpcException)
        {
            this.SetStatus("Service offline", connected: false);
        }
    }

    private void SetStatus(string text, bool connected)
    {
        this.StatusText.Text = text;
        this.StatusDot.Fill = new SolidColorBrush(connected
            ? Color.FromArgb(255, 56, 142, 60)
            : Color.FromArgb(255, 201, 138, 19));
    }
}
