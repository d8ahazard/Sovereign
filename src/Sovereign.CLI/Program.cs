using System.Reflection;

// Milestone 0 diagnostics-only CLI. No privileged action, no network, no service calls yet.
string version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? "0.0.0";

string command = args.Length > 0 ? args[0] : "help";

switch (command)
{
    case "version":
    case "--version":
    case "-v":
        Console.WriteLine($"sov {version}");
        return 0;

    case "help":
    case "--help":
    case "-h":
        PrintUsage();
        return 0;

    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
}

static void PrintUsage()
{
    Console.WriteLine("sov - Sovereign local administration CLI (Milestone 0 scaffold)");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  sov version    Print the CLI version.");
    Console.WriteLine("  sov help       Show this help.");
    Console.WriteLine();
    Console.WriteLine("No privileged commands are available in Milestone 0.");
}
