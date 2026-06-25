using Microsoft.Extensions.Logging.Abstractions;
using Sovereign.Contracts;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;
using Sovereign.Policy;
using Sovereign.Service;
using Sovereign.Storage;
using Xunit;

namespace Sovereign.IntegrationTests;

/// <summary>
/// End-to-end IPC integration tests over a real named-pipe server and the production
/// <see cref="IpcClient"/> (ADR 0002, agent_start.md section 12.2). The server runs on a unique
/// pipe name so it never collides with an installed service.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable fields are released by xUnit's IAsyncLifetime.DisposeAsync.")]
public sealed class IpcEndToEndTests : IAsyncLifetime
{
    private readonly string _pipeName = "Sovereign.Test." + Guid.NewGuid().ToString("N");
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sovereign-ipc-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new();
    private SqliteEventStore _store = null!;
    private SqliteRestorePointStore _restore = null!;
    private NamedPipeServer _server = null!;

    public async Task InitializeAsync()
    {
        this._store = new SqliteEventStore(this._dbPath);
        await this._store.InitializeAsync(CancellationToken.None);
        this._restore = new SqliteRestorePointStore(this._dbPath);
        await this._restore.InitializeAsync(CancellationToken.None);

        var runtime = new ServiceRuntime();
        var catalog = new PolicyCatalog(DemoPolicies.CreateDefault());
        var engine = new PolicyEngine(new InMemorySettingProvider(), this._restore, this._store);
        var dispatcher = new IpcDispatcher(this._store, runtime, new AuthorizationPolicy(), engine, catalog, NullLogger<IpcDispatcher>.Instance);
        this._server = new NamedPipeServer(
            new PipeServerOptions { PipeName = this._pipeName },
            dispatcher,
            this._store,
            runtime,
            NullLogger<NamedPipeServer>.Instance);

        await this._server.StartAsync(this._cts.Token);
    }

    private Task<IpcClient> ConnectAsync() =>
        IpcClient.ConnectAsync("integration-test", this._pipeName, TimeSpan.FromSeconds(10));

    [Fact]
    public async Task Connect_NegotiatesProtocol_AndPingSucceeds()
    {
        await using IpcClient client = await this.ConnectAsync();

        Assert.Equal(IpcContract.CurrentProtocolVersion, client.AgreedProtocolVersion);
        await client.PingAsync();
    }

    [Fact]
    public async Task GetHealth_ReportsRunning()
    {
        await using IpcClient client = await this.ConnectAsync();

        HealthStatus health = await client.GetHealthAsync();

        Assert.Equal("Running", health.State);
        Assert.Equal(IpcContract.CurrentProtocolVersion, health.ProtocolVersion);
    }

    [Fact]
    public async Task QueryEvents_ReturnsConnectionAuditEvent()
    {
        await using IpcClient client = await this.ConnectAsync();

        QueryEventsResponse events = await client.QueryEventsAsync(limit: 100);

        Assert.Contains(events.Events, e => e.Category == "ipc.connect");
    }

    [Fact]
    public async Task Policy_PlanApplyDetectRollback_RoundTripsOverIpc()
    {
        await using IpcClient client = await this.ConnectAsync();

        PolicyListResult list = await client.ListPoliciesAsync();
        Assert.NotEmpty(list.Policies);
        string id = list.Policies[0].Id;

        // Initially non-compliant with a non-empty plan.
        PolicyDetectResult before = await client.DetectPolicyAsync(id);
        Assert.Equal(PolicyResultState.NonCompliant, before.State);
        PolicyPlanInfo plan = await client.PlanPolicyAsync(id);
        Assert.NotEmpty(plan.Changes);

        // Apply succeeds and the policy becomes compliant.
        PolicyRunResult applied = await client.ApplyPolicyAsync(id);
        Assert.Equal(PolicyResultState.Applied, applied.State);
        PolicyDetectResult afterApply = await client.DetectPolicyAsync(id);
        Assert.Equal(PolicyResultState.Compliant, afterApply.State);

        // Re-applying is idempotent (no-op compliant).
        PolicyRunResult reapplied = await client.ApplyPolicyAsync(id);
        Assert.Equal(PolicyResultState.Compliant, reapplied.State);
        Assert.Empty(reapplied.Changes);

        // Rollback restores the captured original state, so the policy is non-compliant again.
        PolicyRunResult rolledBack = await client.RollbackPolicyAsync(id);
        Assert.Equal(PolicyResultState.Applied, rolledBack.State);
        PolicyDetectResult afterRollback = await client.DetectPolicyAsync(id);
        Assert.Equal(PolicyResultState.NonCompliant, afterRollback.State);
    }

    [Fact]
    public async Task ServiceSurvivesClientDisconnect_NewClientStillWorks()
    {
        // First client connects and disconnects (simulating the UI going away).
        await using (IpcClient first = await this.ConnectAsync())
        {
            await first.PingAsync();
        }

        // The service must keep running and serve a fresh connection.
        await using IpcClient second = await this.ConnectAsync();
        HealthStatus health = await second.GetHealthAsync();

        Assert.Equal("Running", health.State);
    }

    public async Task DisposeAsync()
    {
        await this._cts.CancelAsync();
        try
        {
            await this._server.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }

        await this._restore.DisposeAsync();
        await this._store.DisposeAsync();
        this._cts.Dispose();

        TryDelete(this._dbPath);
        TryDelete(this._dbPath + "-wal");
        TryDelete(this._dbPath + "-shm");
    }

    private static void TryDelete(string path)
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
