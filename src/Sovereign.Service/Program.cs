using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sovereign.Policy;
using Sovereign.Service;
using Sovereign.Storage;

// Host bootstrap. Runs as a Windows service (LocalSystem) or a console process (for development).
// Hosts the local SQLite event store, restore-point store, the declarative policy engine, and the
// secured named-pipe IPC endpoint. The engine is backed by the real Windows registry provider and
// the machine-wide Windows policy catalog. Nothing is changed until a caller explicitly applies a
// policy; every apply captures a restore point first so it stays reversible (ADR 0004/0005).
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Sovereign");

builder.Services.AddSingleton<ServiceRuntime>();
builder.Services.AddSingleton<AuthorizationPolicy>();
builder.Services.AddSingleton(new PipeServerOptions());
builder.Services.AddSingleton<IEventStore>(_ => new SqliteEventStore(ServicePaths.DatabasePath));
builder.Services.AddSingleton<IRestorePointStore>(_ => new SqliteRestorePointStore(ServicePaths.DatabasePath));

// Declarative policy engine over the real Windows registry provider (ADR 0004). Reads/writes are
// machine-wide HKLM policy values, captured before change and reversible.
builder.Services.AddSingleton<ISettingProvider, RegistrySettingProvider>();
builder.Services.AddSingleton(_ => new PolicyCatalog(WindowsPolicies.CreateDefault()));
builder.Services.AddSingleton(sp => new PolicyEngine(
    sp.GetRequiredService<ISettingProvider>(),
    sp.GetRequiredService<IRestorePointStore>(),
    sp.GetRequiredService<IEventStore>()));

// App inventory and removal for the debloat experience: Appx/MSIX packages and classic Win32 programs.
builder.Services.AddSingleton<AppxManager>();
builder.Services.AddSingleton<Win32ProgramManager>();

builder.Services.AddSingleton<IpcDispatcher>();

// Order matters: initialize storage before the IPC server starts accepting requests.
builder.Services.AddHostedService<StorageInitializer>();
builder.Services.AddHostedService<NamedPipeServer>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
