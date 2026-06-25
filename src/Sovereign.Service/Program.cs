using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sovereign.Policy;
using Sovereign.Service;
using Sovereign.Storage;

// Milestone 2 host bootstrap. Runs as a Windows service or a console process (for development).
// Hosts the local SQLite event store, restore-point store, the declarative policy engine (over an
// in-memory sandbox provider), and the secured named-pipe IPC endpoint. No real network, registry,
// or Appx changes are performed yet; policies act only on the sandbox provider.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Sovereign");

builder.Services.AddSingleton<ServiceRuntime>();
builder.Services.AddSingleton<AuthorizationPolicy>();
builder.Services.AddSingleton(new PipeServerOptions());
builder.Services.AddSingleton<IEventStore>(_ => new SqliteEventStore(ServicePaths.DatabasePath));
builder.Services.AddSingleton<IRestorePointStore>(_ => new SqliteRestorePointStore(ServicePaths.DatabasePath));

// Declarative policy engine over a harmless in-memory sandbox provider (ADR 0004). Real
// registry/Appx providers arrive in Milestone 5 behind the same ISettingProvider seam.
builder.Services.AddSingleton<ISettingProvider, InMemorySettingProvider>();
builder.Services.AddSingleton(_ => new PolicyCatalog(DemoPolicies.CreateDefault()));
builder.Services.AddSingleton(sp => new PolicyEngine(
    sp.GetRequiredService<ISettingProvider>(),
    sp.GetRequiredService<IRestorePointStore>(),
    sp.GetRequiredService<IEventStore>()));

builder.Services.AddSingleton<IpcDispatcher>();

// Order matters: initialize storage before the IPC server starts accepting requests.
builder.Services.AddHostedService<StorageInitializer>();
builder.Services.AddHostedService<NamedPipeServer>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
