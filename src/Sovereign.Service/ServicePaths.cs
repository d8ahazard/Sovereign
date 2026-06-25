namespace Sovereign.Service;

/// <summary>
/// Well-known local paths used by the service. All state is machine-local under ProgramData.
/// </summary>
internal static class ServicePaths
{
    /// <summary>The Sovereign data directory under the common application-data folder.</summary>
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Sovereign");

    /// <summary>Full path to the local SQLite event-store database.</summary>
    public static string DatabasePath { get; } = Path.Combine(DataDirectory, "sovereign.db");
}
