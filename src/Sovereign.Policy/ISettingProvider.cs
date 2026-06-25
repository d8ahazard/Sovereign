namespace Sovereign.Policy;

/// <summary>
/// The seam between the policy engine and the underlying system state it reads and mutates.
/// </summary>
/// <remarks>
/// Milestone 2 ships only an in-memory sandbox provider, so the engine performs no real machine
/// changes. Milestone 5 adds registry/Appx-backed providers behind this same interface; the engine
/// and its transactional/rollback logic are reused unchanged.
/// </remarks>
public interface ISettingProvider
{
    /// <summary>
    /// Reads the current value of a setting.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The current value, or <see cref="SettingValue.Absent"/> if not set.</returns>
    ValueTask<SettingValue> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a setting to the desired value, where <see cref="SettingValue.Absent"/> removes it.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to write (absent removes the key).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    ValueTask SetAsync(string key, SettingValue value, CancellationToken cancellationToken);
}
