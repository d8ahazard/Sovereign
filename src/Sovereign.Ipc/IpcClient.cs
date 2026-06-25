using System.IO.Pipes;
using System.Text.Json;
using Sovereign.Contracts.Ipc;

namespace Sovereign.Ipc;

/// <summary>
/// Client for the local named-pipe IPC channel. Connects, negotiates a protocol version, and
/// issues read-only and policy operations (list/detect/plan/apply/rollback). Used by both the UI
/// and the CLI so neither references privileged projects directly (agent_start.md section 4,
/// ADR 0002).
/// </summary>
public sealed class IpcClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private long _nextRequestId;

    private IpcClient(NamedPipeClientStream pipe, int agreedProtocolVersion, string serviceVersion)
    {
        this._pipe = pipe;
        this.AgreedProtocolVersion = agreedProtocolVersion;
        this.ServiceVersion = serviceVersion;
    }

    /// <summary>The protocol version negotiated with the service.</summary>
    public int AgreedProtocolVersion { get; }

    /// <summary>The service version reported during the hello exchange.</summary>
    public string ServiceVersion { get; }

    /// <summary>
    /// Connects to the service pipe and performs protocol-version negotiation.
    /// </summary>
    /// <param name="clientName">A short client name for the service audit log.</param>
    /// <param name="pipeName">The pipe name (defaults to the contract pipe name).</param>
    /// <param name="connectTimeout">How long to wait for the pipe to become available.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A connected, negotiated <see cref="IpcClient"/>.</returns>
    /// <exception cref="IpcException">Connection or negotiation failed.</exception>
    public static async Task<IpcClient> ConnectAsync(
        string clientName,
        string pipeName = IpcContract.PipeName,
        TimeSpan? connectTimeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            int timeoutMs = (int)(connectTimeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds;
            await pipe.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);

            var hello = new HelloRequest(
                IpcContract.ProtocolVersionMin,
                IpcContract.ProtocolVersionMax,
                clientName);

            await WriteAsync(pipe, hello, IpcJsonContext.Default.HelloRequest, cancellationToken).ConfigureAwait(false);
            HelloResponse response = await ReadAsync(pipe, IpcJsonContext.Default.HelloResponse, cancellationToken).ConfigureAwait(false);

            if (!response.Accepted)
            {
                throw new IpcException(
                    response.RejectReason ?? "The service rejected protocol negotiation.",
                    IpcErrorCode.ProtocolVersionUnsupported);
            }

            return new IpcClient(pipe, response.AgreedProtocolVersion, response.ServiceVersion);
        }
        catch (Exception ex) when (ex is not IpcException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw new IpcException("Failed to connect to the Sovereign service.", ex);
        }
    }

    /// <summary>Sends a liveness check.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.Ping, Query: null), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
    }

    /// <summary>Gets the service health snapshot.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.GetHealth, Query: null), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Health ?? throw new IpcException("Service returned no health payload.");
    }

    /// <summary>Gets the service version string.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.GetVersion, Query: null), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Version ?? throw new IpcException("Service returned no version payload.");
    }

    /// <summary>Queries recent audit events.</summary>
    /// <param name="limit">Maximum events to return.</param>
    /// <param name="afterId">When set, only events with a greater id are returned.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<QueryEventsResponse> QueryEventsAsync(int limit, long? afterId = null, CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.QueryEvents, new QueryEventsRequest(limit, afterId)),
            cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Events ?? new QueryEventsResponse(Array.Empty<EventRecord>());
    }

    /// <summary>Lists the managed policies.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<PolicyListResult> ListPoliciesAsync(CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.ListPolicies, Query: null), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Policies ?? new PolicyListResult(Array.Empty<PolicyInfo>());
    }

    /// <summary>Detects the current state of a policy.</summary>
    /// <param name="policyId">The policy id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<PolicyDetectResult> DetectPolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.DetectPolicy, Query: null, new PolicyTargetRequest(policyId)), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Detect ?? throw new IpcException("Service returned no detect payload.");
    }

    /// <summary>Returns a plan preview for a policy.</summary>
    /// <param name="policyId">The policy id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<PolicyPlanInfo> PlanPolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.PlanPolicy, Query: null, new PolicyTargetRequest(policyId)), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Plan ?? throw new IpcException("Service returned no plan payload.");
    }

    /// <summary>Applies a policy (mutating).</summary>
    /// <param name="policyId">The policy id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<PolicyRunResult> ApplyPolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.ApplyPolicy, Query: null, new PolicyTargetRequest(policyId)), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.PolicyRun ?? throw new IpcException("Service returned no policy-run payload.");
    }

    /// <summary>Rolls a policy back to its last restore point (mutating).</summary>
    /// <param name="policyId">The policy id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<PolicyRunResult> RollbackPolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.RollbackPolicy, Query: null, new PolicyTargetRequest(policyId)), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.PolicyRun ?? throw new IpcException("Service returned no policy-run payload.");
    }

    /// <summary>Lists the most recent restore points the service has captured.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<RestorePointListResult> ListRestorePointsAsync(CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.ListRestorePoints, Query: null), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.RestorePoints ?? new RestorePointListResult(Array.Empty<RestorePointInfo>());
    }

    /// <summary>Lists installed apps (Appx/MSIX packages) for review and removal.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<AppListResult> ListAppsAsync(CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.ListApps, Query: null), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Apps ?? new AppListResult(Array.Empty<AppInfo>());
    }

    /// <summary>Removes an installed app for all users (mutating).</summary>
    /// <param name="packageFullName">The package full name to remove.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<AppActionResult> RemoveAppAsync(string packageFullName, CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.RemoveApp, Query: null, AppTarget: new AppTargetRequest(packageFullName)), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.AppAction ?? throw new IpcException("Service returned no app-action payload.");
    }

    /// <summary>Lists installed classic (Win32) programs for review and removal.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<AppListResult> ListProgramsAsync(CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.ListPrograms, Query: null), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.Apps ?? new AppListResult(Array.Empty<AppInfo>());
    }

    /// <summary>Uninstalls a classic (Win32) program via its registered uninstaller (mutating).</summary>
    /// <param name="programId">The program id from <see cref="ListProgramsAsync"/>.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<AppActionResult> RemoveProgramAsync(string programId, CancellationToken cancellationToken = default)
    {
        ResponseEnvelope response = await SendAsync(
            new RequestEnvelope(this.NextId(), IpcOperation.RemoveProgram, Query: null, AppTarget: new AppTargetRequest(programId)), cancellationToken).ConfigureAwait(false);
        ThrowIfError(response);
        return response.AppAction ?? throw new IpcException("Service returned no app-action payload.");
    }

    private async Task<ResponseEnvelope> SendAsync(RequestEnvelope request, CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(this._pipe, request, IpcJsonContext.Default.RequestEnvelope, cancellationToken).ConfigureAwait(false);
            return await ReadAsync(this._pipe, IpcJsonContext.Default.ResponseEnvelope, cancellationToken).ConfigureAwait(false);
        }
        catch (IpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IpcException("IPC transport failure.", ex);
        }
    }

    private long NextId() => Interlocked.Increment(ref this._nextRequestId);

    private static void ThrowIfError(ResponseEnvelope response)
    {
        if (response.ErrorCode != IpcErrorCode.None)
        {
            throw new IpcException(response.Message ?? response.ErrorCode.ToString(), response.ErrorCode);
        }
    }

    private static async ValueTask WriteAsync<T>(Stream stream, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        await IpcFraming.WriteFrameAsync(stream, body, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> ReadAsync<T>(Stream stream, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        byte[]? body = await IpcFraming.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            throw new IpcException("The service closed the connection unexpectedly.");
        }

        T? value = JsonSerializer.Deserialize(body, typeInfo);
        return value ?? throw new IpcException("The service returned an empty or invalid message.", IpcErrorCode.BadRequest);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this._pipe.DisposeAsync().ConfigureAwait(false);
    }
}
