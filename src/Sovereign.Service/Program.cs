using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sovereign.Service;
using Sovereign.Storage;

// Milestone 1 host bootstrap. Runs as a Windows service or a console process (for development).
// Hosts the local SQLite event store and the secured named-pipe IPC endpoint. No network,
// registry, or policy enforcement is performed yet.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Sovereign");

builder.Services.AddSingleton<ServiceRuntime>();
builder.Services.AddSingleton<AuthorizationPolicy>();
builder.Services.AddSingleton(new PipeServerOptions());
builder.Services.AddSingleton<IEventStore>(_ => new SqliteEventStore(ServicePaths.DatabasePath));
builder.Services.AddSingleton<IpcDispatcher>();

// Order matters: initialize storage before the IPC server starts accepting requests.
builder.Services.AddHostedService<StorageInitializer>();
builder.Services.AddHostedService<NamedPipeServer>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
