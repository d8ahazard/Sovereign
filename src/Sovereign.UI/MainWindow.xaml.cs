using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;

namespace Sovereign.UI;

/// <summary>
/// A single recent-activity row shown in the dashboard list.
/// </summary>
/// <param name="Time">Formatted timestamp.</param>
/// <param name="Category">Event category.</param>
/// <param name="Message">Event message.</param>
public sealed record EventRow(string Time, string Category, string Message);

/// <summary>
/// The main dashboard window. It connects to the service over the authenticated IPC client and
/// shows health plus recent activity. If the service is unavailable it degrades to a clear,
/// friendly offline state instead of failing (agent_start.md section 9).
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<EventRow> _events = [];

    /// <summary>Initializes the window and kicks off the first refresh.</summary>
    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "Sovereign";
        this.EventsList.ItemsSource = this._events;
        _ = this.RefreshAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await this.RefreshAsync().ConfigureAwait(true);
    }

    private async Task RefreshAsync()
    {
        this.RefreshButton.IsEnabled = false;
        this.SetBadge("Connecting...", connected: false);

        try
        {
            await using IpcClient client = await IpcClient.ConnectAsync("sovereign-ui", connectTimeout: TimeSpan.FromSeconds(5)).ConfigureAwait(true);

            HealthStatus health = await client.GetHealthAsync().ConfigureAwait(true);
            QueryEventsResponse events = await client.QueryEventsAsync(limit: 100).ConfigureAwait(true);

            this.StateText.Text = health.State;
            this.VersionText.Text = health.ServiceVersion;
            this.UptimeText.Text = health.UptimeSeconds.ToString(CultureInfo.CurrentCulture);
            this.EventCountText.Text = health.EventCount.ToString(CultureInfo.CurrentCulture);

            this._events.Clear();
            foreach (EventRecord record in events.Events)
            {
                this._events.Insert(0, new EventRow(
                    record.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
                    record.Category,
                    record.Message));
            }

            this.SetBadge("Connected", connected: true);
            this.StatusBar.IsOpen = false;
        }
        catch (IpcException ex)
        {
            this.SetBadge("Offline", connected: false);
            this.ShowOffline(ex.Message);
        }
        finally
        {
            this.RefreshButton.IsEnabled = true;
        }
    }

    private void SetBadge(string text, bool connected)
    {
        this.ConnectionBadgeText.Text = text;
        string resource = connected ? "SystemFillColorSuccessBackgroundBrush" : "SystemFillColorCautionBackgroundBrush";
        if (Application.Current.Resources.TryGetValue(resource, out object? brush)
            && brush is Microsoft.UI.Xaml.Media.Brush typed)
        {
            this.ConnectionBadge.Background = typed;
        }
    }

    private void ShowOffline(string detail)
    {
        this.StateText.Text = "Offline";
        this.VersionText.Text = "-";
        this.UptimeText.Text = "-";
        this.EventCountText.Text = "-";
        this._events.Clear();

        this.StatusBar.Severity = InfoBarSeverity.Warning;
        this.StatusBar.Title = "Service not reachable";
        this.StatusBar.Message = $"{detail} Start the Sovereign service, then click Refresh.";
        this.StatusBar.IsOpen = true;
    }
}
