using System.Collections.Concurrent;
using Sovereign.Contracts.Ipc;
using Sovereign.Policy;
using Sovereign.Storage;

namespace Sovereign.UnitTests;

/// <summary>An in-memory <see cref="IEventStore"/> that records appends for assertions.</summary>
internal sealed class RecordingEventStore : IEventStore
{
    private readonly List<EventRecord> _events = [];
    private long _id;

    public IReadOnlyList<EventRecord> Events => this._events;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<long> AppendAsync(string category, string message, CancellationToken cancellationToken)
    {
        long id = ++this._id;
        this._events.Add(new EventRecord(id, DateTimeOffset.UtcNow, category, message));
        return ValueTask.FromResult(id);
    }

    public ValueTask<IReadOnlyList<EventRecord>> QueryAsync(int limit, long? afterId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<EventRecord>>(this._events.Where(e => e.Id > (afterId ?? 0)).Take(limit).ToArray());

    public ValueTask<long> CountAsync(CancellationToken cancellationToken) => ValueTask.FromResult((long)this._events.Count);
}

/// <summary>An in-memory <see cref="IRestorePointStore"/> for tests.</summary>
internal sealed class InMemoryRestorePointStore : IRestorePointStore
{
    private readonly ConcurrentDictionary<string, RestorePoint> _latest = new(StringComparer.Ordinal);
    private long _id;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask<long> SaveAsync(string policyId, string correlationId, string payloadJson, CancellationToken cancellationToken)
    {
        long id = Interlocked.Increment(ref this._id);
        this._latest[policyId] = new RestorePoint(id, policyId, correlationId, DateTimeOffset.UtcNow, payloadJson);
        return ValueTask.FromResult(id);
    }

    public ValueTask<RestorePoint?> GetLatestAsync(string policyId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(this._latest.TryGetValue(policyId, out RestorePoint? point) ? point : null);

    public ValueTask<IReadOnlyList<RestorePoint>> QueryAsync(int limit, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<RestorePoint>>(
            this._latest.Values.OrderByDescending(p => p.Id).Take(Math.Clamp(limit, 1, 500)).ToArray());
}

/// <summary>
/// An <see cref="ISettingProvider"/> that can be told to fail in specific ways, for exercising the
/// engine's transactional and rollback paths.
/// </summary>
internal sealed class FaultySettingProvider : ISettingProvider
{
    private readonly InMemorySettingProvider _inner = new();

    /// <summary>Keys whose reads throw (drives the Unknown path).</summary>
    public HashSet<string> FailGetKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Keys whose writes of a present value throw (drives partial-failure rollback).</summary>
    public HashSet<string> FailSetPresentKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Keys whose writes are silently ignored (drives the verify-failure path).</summary>
    public HashSet<string> IgnoreSetKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Keys whose writes of an absent value throw (drives the rollback-failed path).</summary>
    public HashSet<string> FailClearKeys { get; } = new(StringComparer.Ordinal);

    public async ValueTask<SettingValue> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (this.FailGetKeys.Contains(key))
        {
            throw new InvalidOperationException($"Injected read failure for '{key}'.");
        }

        return await this._inner.GetAsync(key, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetAsync(string key, SettingValue value, CancellationToken cancellationToken)
    {
        if (value.Exists && this.FailSetPresentKeys.Contains(key))
        {
            throw new InvalidOperationException($"Injected write failure for '{key}'.");
        }

        if (!value.Exists && this.FailClearKeys.Contains(key))
        {
            throw new InvalidOperationException($"Injected clear failure for '{key}'.");
        }

        if (this.IgnoreSetKeys.Contains(key))
        {
            return;
        }

        await this._inner.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Small helpers for building test policies.</summary>
internal static class TestPolicy
{
    public static IPolicy Create(string id, IReadOnlyList<DesiredSetting> settings, bool supported = true) =>
        new DeclarativeSettingPolicy(
            new PolicyMetadata(id, 1, id, id, Sovereign.Contracts.PolicyRiskLevel.Low, Sovereign.Contracts.PolicyScope.Machine),
            settings,
            _ => ValueTask.FromResult(supported));

    public static DesiredSetting Present(string key, string value) =>
        new(key, SettingValue.Present(value), $"{key} should be {value}.");

    public static DesiredSetting Absent(string key) =>
        new(key, SettingValue.Absent, $"{key} should be absent.");
}
