using Microsoft.Extensions.Logging.Abstractions;
using Sovereign.Contracts.Ipc;
using Sovereign.Policy;
using Sovereign.Service;
using Sovereign.Storage;
using Xunit;

namespace Sovereign.SecurityTests;

/// <summary>
/// Security tests for the IPC authorization boundary (agent_start.md sections 7 and 15.2,
/// ADR 0002). Operations outside the explicit allow-list must be denied (fail closed) and audited.
/// Milestone 2 adds mutating operations (ApplyPolicy/RollbackPolicy); these stay behind the allow-list
/// and must be audited with the caller identity.
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

    [Fact]
    public async Task Dispatch_MutatingApplyOperation_IsAuditedWithCaller()
    {
        await using SqliteEventStore store = await this.CreateStoreAsync();
        IpcDispatcher dispatcher = CreateDispatcher(store);

        string policyId = DemoPolicies.CreateDefault()[0].Metadata.Id;
        var request = new RequestEnvelope(1, IpcOperation.ApplyPolicy, Query: null, new PolicyTargetRequest(policyId));
        ResponseEnvelope response = await dispatcher.DispatchAsync(request, new CallerContext("auditor"), CancellationToken.None);

        Assert.Equal(IpcErrorCode.None, response.ErrorCode);

        IReadOnlyList<EventRecord> events = await store.QueryAsync(100, afterId: null, CancellationToken.None);
        Assert.Contains(events, e => e.Category == "policy.apply.requested" && e.Message.Contains("auditor", StringComparison.Ordinal));
    }

    private async Task<SqliteEventStore> CreateStoreAsync()
    {
        var store = new SqliteEventStore(this._dbPath);
        await store.InitializeAsync(CancellationToken.None);
        return store;
    }

    private static IpcDispatcher CreateDispatcher(IEventStore store)
    {
        var catalog = new PolicyCatalog(DemoPolicies.CreateDefault());
        var restore = new InMemoryRestorePointStore();
        var engine = new PolicyEngine(new InMemorySettingProvider(), restore, store);
        return new IpcDispatcher(store, new ServiceRuntime(), new AuthorizationPolicy(), engine, catalog, restore, new AppxManager(NullLogger<AppxManager>.Instance), new Win32ProgramManager(NullLogger<Win32ProgramManager>.Instance), NullLogger<IpcDispatcher>.Instance);
    }

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
