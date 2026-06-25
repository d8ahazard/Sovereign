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

/// <summary>
/// The landing page: service health at a glance plus a one-click route into Cleanup, with a live
/// summary of how many recommended tweaks are already applied.
/// </summary>
public sealed partial class DashboardPage : Page
{
    /// <summary>Initializes the page.</summary>
    public DashboardPage()
    {
        this.InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = this.RefreshAsync();
    }

    private void OnOpenCleanup(object sender, RoutedEventArgs e) => App.Shell?.NavigateToCleanup();

    private async void OnRefresh(object sender, RoutedEventArgs e) => await this.RefreshAsync().ConfigureAwait(true);

    private async Task RefreshAsync()
    {
        try
        {
            HealthStatus health = await SovereignClient.RunAsync(c => c.GetHealthAsync()).ConfigureAwait(true);
            this.StateText.Text = health.State;
            this.VersionText.Text = health.ServiceVersion;
            this.UptimeText.Text = health.UptimeSeconds.ToString(CultureInfo.CurrentCulture);
            this.EventCountText.Text = health.EventCount.ToString(CultureInfo.CurrentCulture);
            this.OfflineBar.IsOpen = false;

            await this.UpdateComplianceAsync().ConfigureAwait(true);
        }
        catch (IpcException)
        {
            this.StateText.Text = "Offline";
            this.VersionText.Text = "-";
            this.UptimeText.Text = "-";
            this.EventCountText.Text = "-";
            this.ComplianceSummary.Text = "Connect to the service to see what's applied.";
            this.OfflineBar.IsOpen = true;
        }
    }

    private async Task UpdateComplianceAsync()
    {
        try
        {
            int applied = 0;
            int total = 0;

            await SovereignClient.RunAsync(async client =>
            {
                PolicyListResult list = await client.ListPoliciesAsync().ConfigureAwait(false);
                foreach (PolicyInfo policy in list.Policies)
                {
                    total++;
                    PolicyDetectResult detect = await client.DetectPolicyAsync(policy.Id).ConfigureAwait(false);
                    if (detect.State == PolicyResultState.Compliant)
                    {
                        applied++;
                    }
                }
            }).ConfigureAwait(true);

            this.ComplianceSummary.Text = applied == total && total > 0
                ? $"All {total} tweaks are applied. Nice and clean."
                : $"{applied} of {total} tweaks applied. Open Cleanup to choose a hardening level and apply the rest.";
        }
        catch (IpcException)
        {
            this.ComplianceSummary.Text = "Connect to the service to see what's applied.";
        }
    }
}
