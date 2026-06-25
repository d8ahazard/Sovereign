using Microsoft.UI.Xaml;

namespace Sovereign.UI;

/// <summary>
/// The unelevated WinUI 3 application entry point (ADR 0003). It owns no privileged state; all
/// service interaction flows through the authenticated IPC client.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>Initializes the application.</summary>
    public App()
    {
        this.InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        this._window = new MainWindow();
        this._window.Activate();
    }
}
