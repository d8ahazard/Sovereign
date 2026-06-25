using Microsoft.Extensions.Logging;
using Sovereign.Contracts.Ipc;
using Sovereign.Storage;

namespace Sovereign.Service;

/// <summary>
/// Translates an authenticated <see cref="RequestEnvelope"/> into a <see cref="ResponseEnvelope"/>,
/// enforcing the authorization allow-list. Transport-free so it can be unit-tested directly
/// (ADR 0002).
/// </summary>
public sealed partial class IpcDispatcher(
    IEventStore eventStore,
    ServiceRuntime runtime,
    AuthorizationPolicy authorization,
    ILogger<IpcDispatcher> logger)
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly ServiceRuntime _runtime = runtime;
    private readonly AuthorizationPolicy _authorization = authorization;
    private readonly ILogger<IpcDispatcher> _logger = logger;

    /// <summary>
    /// Dispatches a single request. Unknown or unauthorized operations fail closed with a
    /// non-success <see cref="IpcErrorCode"/>.
    /// </summary>
    /// <param name="request">The caller's request envelope.</param>
    /// <param name="caller">The authenticated caller context (for auditing).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task<ResponseEnvelope> DispatchAsync(RequestEnvelope request, CallerContext caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!this._authorization.IsAllowed(request.Operation))
        {
            LogDenied(this._logger, request.Operation, caller.UserNameOrUnknown);
            await this.TryAuditAsync("ipc.denied", $"Operation {request.Operation} denied for {caller.UserNameOrUnknown}.", cancellationToken).ConfigureAwait(false);
            return Error(request.RequestId, IpcErrorCode.Unauthorized, $"Operation {request.Operation} is not permitted.");
        }

        try
        {
            return request.Operation switch
            {
                IpcOperation.Ping => Ok(request.RequestId),
                IpcOperation.GetVersion => Ok(request.RequestId) with { Version = this._runtime.Version },
                IpcOperation.GetHealth => Ok(request.RequestId) with { Health = await this.BuildHealthAsync(cancellationToken).ConfigureAwait(false) },
                IpcOperation.QueryEvents => Ok(request.RequestId) with { Events = await this.QueryEventsAsync(request.Query, cancellationToken).ConfigureAwait(false) },
                _ => Error(request.RequestId, IpcErrorCode.UnknownOperation, "Unknown operation."),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailed(this._logger, ex, request.Operation);
            return Error(request.RequestId, IpcErrorCode.InternalError, "The operation failed.");
        }
    }

    private async Task<HealthStatus> BuildHealthAsync(CancellationToken cancellationToken)
    {
        long count = await this._eventStore.CountAsync(cancellationToken).ConfigureAwait(false);
        return new HealthStatus(
            this._runtime.Version,
            this._runtime.ProtocolVersion,
            "Running",
            this._runtime.StartedUtc,
            this._runtime.UptimeSeconds,
            count);
    }

    private async Task<QueryEventsResponse> QueryEventsAsync(QueryEventsRequest? query, CancellationToken cancellationToken)
    {
        int limit = query?.Limit ?? 100;
        long? afterId = query?.AfterId;
        IReadOnlyList<EventRecord> events = await this._eventStore.QueryAsync(limit, afterId, cancellationToken).ConfigureAwait(false);
        return new QueryEventsResponse(events);
    }

    private async Task TryAuditAsync(string category, string message, CancellationToken cancellationToken)
    {
        try
        {
            await this._eventStore.AppendAsync(category, message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogAuditFailed(this._logger, ex);
        }
    }

    private static ResponseEnvelope Ok(long requestId) =>
        new(requestId, IpcErrorCode.None, Message: null, Health: null, Events: null, Version: null);

    private static ResponseEnvelope Error(long requestId, IpcErrorCode code, string message) =>
        new(requestId, code, message, Health: null, Events: null, Version: null);

    [LoggerMessage(EventId = 100, Level = LogLevel.Warning, Message = "IPC operation {Operation} denied for {User}.")]
    private static partial void LogDenied(ILogger logger, IpcOperation operation, string user);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error, Message = "IPC operation {Operation} failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception, IpcOperation operation);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "Failed to write audit event.")]
    private static partial void LogAuditFailed(ILogger logger, Exception exception);
}
