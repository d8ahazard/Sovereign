using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sovereign.Service;

/// <summary>
/// Milestone 0 no-op background worker.
/// </summary>
/// <remarks>
/// This worker exists to establish the privileged-service process boundary. It performs no
/// enforcement, no policy application, and no network or registry changes. It simply logs
/// that the scaffold is running and then idles until shutdown is requested.
/// </remarks>
internal sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(this._logger);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown.
        }

        LogStopping(this._logger);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Sovereign.Service Milestone 0 scaffold started. No enforcement is active.")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Sovereign.Service scaffold stopping.")]
    private static partial void LogStopping(ILogger logger);
}
