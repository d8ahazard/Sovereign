using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace Sovereign.UI;

/// <summary>
/// The unelevated WinUI 3 application entry point (ADR 0003). It owns no privileged state; all
/// service interaction flows through the authenticated IPC client.
/// </summary>
public partial class App : Application
{
    /// <summary>Initializes the application.</summary>
    public App()
    {
        this.InitializeComponent();
    }

    /// <summary>The single shell window, available to pages for cross-page navigation.</summary>
    public static MainWindow? Shell { get; private set; }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        this.UnhandledException += static (_, e) => Log(e.Exception);
        try
        {
            Shell = new MainWindow();
            Shell.Activate();
        }
        catch (Exception ex)
        {
            Log(ex);
            throw;
        }
    }

    private static void Log(Exception ex)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "sovereign-ui-crash.log");
            File.WriteAllText(path, $"{DateTimeOffset.Now:O}{Environment.NewLine}{ex}");
        }
        catch (IOException)
        {
        }
    }
}
