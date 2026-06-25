using Microsoft.Extensions.Logging.Abstractions;
using Sovereign.Contracts.Ipc;
using Sovereign.Service;
using Sovereign.Storage;
using Xunit;

namespace Sovereign.SecurityTests;

/// <summary>
/// Security tests for the IPC authorization boundary (agent_start.md sections 7 and 15.2,
/// ADR 0002). Operations outside the explicit allow-list must be denied (fail closed), and the
/// denial must be audited. Milestone 1 has no privileged operations, so any operation not on the
/// read-only allow-list stands in for an attempted privileged call.
/// </summary>
public sealed class AuthorizationDenialTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sovereign-sec-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Dispatch_OperationNotOnAllowList_IsDenied()
    {
        await using SqliteEventStore store = await this.CreateStoreAsync();
        IpcDispatcher dispatcher = CreateDispatcher(store);

        // A value outside the defined enum represents any future/privileged operation.
        var request = new RequestEnvelope(1, (IpcOperation)9999, Query: null);
        ResponseEnvelope response = await dispatcher.DispatchAsync(request, new CallerContext("tester"), CancellationToken.None);

        Assert.Equal(IpcErrorCode.Unauthorized, response.ErrorCode);
        Assert.Null(response.Health);
        Assert.Null(response.Events);
        Assert.Null(response.Version);
    }

    [Fact]
    public async Task Dispatch_DeniedOperation_IsAudited()
    {
        await using SqliteEventStore store = await this.CreateStoreAsync();
        IpcDispatcher dispatcher = CreateDispatcher(store);

        var request = new RequestEnvelope(1, (IpcOperation)9999, Query: null);
        await dispatcher.DispatchAsync(request, new CallerContext("tester"), CancellationToken.None);

        IReadOnlyList<EventRecord> events = await store.QueryAsync(100, afterId: null, CancellationToken.None);
        Assert.Contains(events, e => e.Category == "ipc.denied");
    }

    [Fact]
    public async Task Dispatch_AllowedReadOnlyOperation_Succeeds()
    {
        await using SqliteEventStore store = await this.CreateStoreAsync();
        IpcDispatcher dispatcher = CreateDispatcher(store);

        var request = new RequestEnvelope(1, IpcOperation.GetVersion, Query: null);
        ResponseEnvelope response = await dispatcher.DispatchAsync(request, new CallerContext("tester"), CancellationToken.None);

        Assert.Equal(IpcErrorCode.None, response.ErrorCode);
        Assert.False(string.IsNullOrEmpty(response.Version));
    }

    private async Task<SqliteEventStore> CreateStoreAsync()
    {
        var store = new SqliteEventStore(this._dbPath);
        await store.InitializeAsync(CancellationToken.None);
        return store;
    }

    private static IpcDispatcher CreateDispatcher(IEventStore store) =>
        new(store, new ServiceRuntime(), new AuthorizationPolicy(), NullLogger<IpcDispatcher>.Instance);

    public void Dispose()
    {
        foreach (string path in new[] { this._dbPath, this._dbPath + "-wal", this._dbPath + "-shm" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
