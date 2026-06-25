using System.Collections.Concurrent;

namespace Sovereign.Policy;

/// <summary>
/// A thread-safe, in-memory <see cref="ISettingProvider"/> used as a harmless sandbox in Milestone 2.
/// </summary>
/// <remarks>
/// This makes no real machine changes: it is the backing store for the initial harmless test
/// policies and for exercising the full detect/plan/apply/verify/rollback engine in tests.
/// </remarks>
public sealed class InMemorySettingProvider : ISettingProvider
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<SettingValue> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        return this._values.TryGetValue(key, out string? value)
            ? ValueTask.FromResult(SettingValue.Present(value))
            : ValueTask.FromResult(SettingValue.Absent);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(string key, SettingValue value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        if (value.Exists)
        {
            this._values[key] = value.Value!;
        }
        else
        {
            this._values.TryRemove(key, out _);
        }

        return ValueTask.CompletedTask;
    }
}
