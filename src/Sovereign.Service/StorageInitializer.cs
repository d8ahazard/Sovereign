using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sovereign.Storage;

namespace Sovereign.Service;

/// <summary>
/// Initializes the local event store before the IPC server begins serving requests, and records a
/// startup event. Registered ahead of <see cref="NamedPipeServer"/> so its <c>StartAsync</c>
/// completes first.
/// </summary>
internal sealed partial class StorageInitializer(
    IEventStore eventStore,
    ServiceRuntime runtime,
    ILogger<StorageInitializer> logger) : IHostedService
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly ServiceRuntime _runtime = runtime;
    private readonly ILogger<StorageInitializer> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await this._eventStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await this._eventStore.AppendAsync("service.start", $"Service started (version {this._runtime.Version}).", cancellationToken).ConfigureAwait(false);
        LogInitialized(this._logger, ServicePaths.DatabasePath);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        LogStopping(this._logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Information, Message = "Event store initialized at '{Path}'.")]
    private static partial void LogInitialized(ILogger logger, string path);

    [LoggerMessage(EventId = 301, Level = LogLevel.Information, Message = "Service stopping.")]
    private static partial void LogStopping(ILogger logger);
}
