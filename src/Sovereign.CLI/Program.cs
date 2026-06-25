using System.Globalization;
using System.Reflection;
using Sovereign.Contracts.Ipc;
using Sovereign.Ipc;

// Milestone 1 diagnostics CLI. Read-only commands that go through the same authenticated IPC
// channel and authorization model as the UI (agent_start.md section 3). It is never a privileged
// bypass: it can only invoke operations the service's allow-list permits.

string cliVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? "0.0.0";

string command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

switch (command)
{
    case "version":
    case "--version":
    case "-v":
        Console.WriteLine($"sov {cliVersion}");
        return 0;

    case "help":
    case "--help":
    case "-h":
        PrintUsage();
        return 0;

    case "status":
        return await RunStatusAsync().ConfigureAwait(false);

    case "health":
        return await RunHealthAsync().ConfigureAwait(false);

    case "events":
        return await RunEventsAsync(args).ConfigureAwait(false);

    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
}

static async Task<int> RunStatusAsync()
{
    return await WithClientAsync(async client =>
    {
        await client.PingAsync().ConfigureAwait(false);
        Console.WriteLine("Connected to Sovereign service.");
        Console.WriteLine($"  Service version : {client.ServiceVersion}");
        Console.WriteLine($"  Protocol        : v{client.AgreedProtocolVersion}");
        return 0;
    }).ConfigureAwait(false);
}

static async Task<int> RunHealthAsync()
{
    return await WithClientAsync(async client =>
    {
        HealthStatus health = await client.GetHealthAsync().ConfigureAwait(false);
        Console.WriteLine("Service health:");
        Console.WriteLine($"  State           : {health.State}");
        Console.WriteLine($"  Version         : {health.ServiceVersion}");
        Console.WriteLine($"  Protocol        : v{health.ProtocolVersion}");
        Console.WriteLine($"  Started (UTC)   : {health.StartedUtc:u}");
        Console.WriteLine($"  Uptime (s)      : {health.UptimeSeconds}");
        Console.WriteLine($"  Events recorded : {health.EventCount}");
        return 0;
    }).ConfigureAwait(false);
}

static async Task<int> RunEventsAsync(string[] args)
{
    int limit = GetIntOption(args, "--limit", 50);
    long? after = GetLongOption(args, "--after");

    return await WithClientAsync(async client =>
    {
        QueryEventsResponse result = await client.QueryEventsAsync(limit, after).ConfigureAwait(false);
        if (result.Events.Count == 0)
        {
            Console.WriteLine("No events.");
            return 0;
        }

        foreach (EventRecord e in result.Events)
        {
            Console.WriteLine($"#{e.Id,-6} {e.TimestampUtc:u}  {e.Category,-16}  {e.Message}");
        }

        return 0;
    }).ConfigureAwait(false);
}

static async Task<int> WithClientAsync(Func<IpcClient, Task<int>> action)
{
    try
    {
        await using IpcClient client = await IpcClient.ConnectAsync("sov-cli").ConfigureAwait(false);
        return await action(client).ConfigureAwait(false);
    }
    catch (IpcException ex)
    {
        Console.Error.WriteLine($"Could not complete the request: {ex.Message}");
        Console.Error.WriteLine("Is the Sovereign service running? (See scripts/install-service.ps1)");
        return 2;
    }
}

static int GetIntOption(string[] args, string name, int fallback)
{
    long? value = GetLongOption(args, name);
    return value is null ? fallback : (int)Math.Clamp(value.Value, int.MinValue, int.MaxValue);
}

static long? GetLongOption(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
        {
            return parsed;
        }
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine("sov - Sovereign local administration CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  sov status                 Connect and show service version/protocol.");
    Console.WriteLine("  sov health                 Show service health (uptime, event count).");
    Console.WriteLine("  sov events [--limit N]     List recent audit events.");
    Console.WriteLine("             [--after ID]");
    Console.WriteLine("  sov version                Print the CLI version.");
    Console.WriteLine("  sov help                   Show this help.");
    Console.WriteLine();
    Console.WriteLine("All commands are read-only and go through the authenticated local IPC channel.");
}
