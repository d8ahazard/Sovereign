using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;
using Sovereign.Storage;

namespace Sovereign.Service;

/// <summary>
/// Hosts the secured named-pipe IPC endpoint. Accepts connections, negotiates a protocol version,
/// captures the authenticated caller identity, and dispatches read-only operations (ADR 0002).
/// </summary>
internal sealed partial class NamedPipeServer(
    PipeServerOptions options,
    IpcDispatcher dispatcher,
    IEventStore eventStore,
    ServiceRuntime runtime,
    ILogger<NamedPipeServer> logger) : BackgroundService
{
    private readonly PipeServerOptions _options = options;
    private readonly IpcDispatcher _dispatcher = dispatcher;
    private readonly IEventStore _eventStore = eventStore;
    private readonly ServiceRuntime _runtime = runtime;
    private readonly ILogger<NamedPipeServer> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogListening(this._logger, this._options.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = NamedPipeServerStreamAcl.Create(
                    this._options.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    PipeSecurityFactory.Create());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCreateFailed(this._logger, ex);
                return;
            }

            try
            {
                await server.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                break;
            }
            catch (Exception ex)
            {
                LogAcceptFailed(this._logger, ex);
                await server.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            // Handle this connection independently; immediately loop to accept the next one.
            _ = this.HandleConnectionAsync(server, stoppingToken);
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken stoppingToken)
    {
        try
        {
            CallerContext caller = new(TryGetCallerName(server));

            if (!await this.HandshakeAsync(server, caller, stoppingToken).ConfigureAwait(false))
            {
                return;
            }

            while (server.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                byte[]? frame = await IpcFraming.ReadFrameAsync(server, stoppingToken).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                ResponseEnvelope response = await this.HandleRequestFrameAsync(frame, caller, stoppingToken).ConfigureAwait(false);
                await WriteAsync(server, response, IpcJsonContext.Default.ResponseEnvelope, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (IOException)
        {
            // Client disconnected abruptly; normal.
        }
        catch (IpcException ex)
        {
            LogProtocolError(this._logger, ex, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            LogConnectionError(this._logger, ex);
        }
        finally
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> HandshakeAsync(NamedPipeServerStream server, CallerContext caller, CancellationToken cancellationToken)
    {
        byte[]? helloFrame = await IpcFraming.ReadFrameAsync(server, cancellationToken).ConfigureAwait(false);
        if (helloFrame is null)
        {
            return false;
        }

        HelloRequest? hello = JsonSerializer.Deserialize(helloFrame, IpcJsonContext.Default.HelloRequest);
        if (hello is null)
        {
            await WriteAsync(
                server,
                new HelloResponse(false, 0, this._runtime.Version, "Malformed hello."),
                IpcJsonContext.Default.HelloResponse,
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        bool ok = ProtocolNegotiation.TryNegotiate(
            hello.ClientProtocolMin,
            hello.ClientProtocolMax,
            IpcContract.ProtocolVersionMin,
            IpcContract.ProtocolVersionMax,
            out int agreed);

        var response = ok
            ? new HelloResponse(true, agreed, this._runtime.Version, null)
            : new HelloResponse(false, 0, this._runtime.Version, "No common protocol version.");

        await WriteAsync(server, response, IpcJsonContext.Default.HelloResponse, cancellationToken).ConfigureAwait(false);

        if (ok)
        {
            await this.TryAuditAsync(
                "ipc.connect",
                $"Client '{hello.ClientName}' connected as {caller.UserNameOrUnknown} (protocol v{agreed}).",
                cancellationToken).ConfigureAwait(false);
        }

        return ok;
    }

    private async Task<ResponseEnvelope> HandleRequestFrameAsync(byte[] frame, CallerContext caller, CancellationToken cancellationToken)
    {
        RequestEnvelope? request;
        try
        {
            request = JsonSerializer.Deserialize(frame, IpcJsonContext.Default.RequestEnvelope);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null)
        {
            return new ResponseEnvelope(0, IpcErrorCode.BadRequest, "Malformed request.", null, null, null);
        }

        return await this._dispatcher.DispatchAsync(request, caller, cancellationToken).ConfigureAwait(false);
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

    private static string? TryGetCallerName(NamedPipeServerStream server)
    {
        try
        {
            return server.GetImpersonationUserName();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async ValueTask WriteAsync<T>(Stream stream, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        await IpcFraming.WriteFrameAsync(stream, body, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Information, Message = "IPC server listening on pipe '{Pipe}'.")]
    private static partial void LogListening(ILogger logger, string pipe);

    [LoggerMessage(EventId = 201, Level = LogLevel.Critical, Message = "Failed to create the IPC pipe; IPC is unavailable.")]
    private static partial void LogCreateFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 202, Level = LogLevel.Warning, Message = "Failed to accept an IPC connection.")]
    private static partial void LogAcceptFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 203, Level = LogLevel.Debug, Message = "IPC protocol error ({Code}).")]
    private static partial void LogProtocolError(ILogger logger, Exception exception, IpcErrorCode code);

    [LoggerMessage(EventId = 204, Level = LogLevel.Warning, Message = "IPC connection error.")]
    private static partial void LogConnectionError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 205, Level = LogLevel.Warning, Message = "Failed to write audit event.")]
    private static partial void LogAuditFailed(ILogger logger, Exception exception);
}
