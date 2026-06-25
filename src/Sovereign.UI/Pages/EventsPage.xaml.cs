using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;
using Sovereign.UI.Services;

namespace Sovereign.UI.Pages;

/// <summary>A single recent-activity row.</summary>
/// <param name="Time">Formatted local timestamp.</param>
/// <param name="Category">Event category.</param>
/// <param name="Message">Event message.</param>
public sealed record EventRow(string Time, string Category, string Message);

/// <summary>The append-only audit log, newest first.</summary>
public sealed partial class EventsPage : Page
{
    private readonly ObservableCollection<EventRow> _events = [];

    /// <summary>Initializes the page.</summary>
    public EventsPage()
    {
        this.InitializeComponent();
        this.EventsList.ItemsSource = this._events;
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
            QueryEventsResponse events = await SovereignClient.RunAsync(c => c.QueryEventsAsync(limit: 200)).ConfigureAwait(true);
            this._events.Clear();
            foreach (EventRecord record in events.Events)
            {
                this._events.Insert(0, new EventRow(
                    record.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
                    record.Category,
                    record.Message));
            }

            this.OfflineBar.IsOpen = false;
        }
        catch (IpcException)
        {
            this._events.Clear();
            this.OfflineBar.IsOpen = true;
        }
    }
}
