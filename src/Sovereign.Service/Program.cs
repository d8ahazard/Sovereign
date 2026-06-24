using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sovereign.Service;

// Milestone 0 host bootstrap. Builds a generic host that can run either as a console
// process or, in later milestones, as a Windows service. No privileged work is performed.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Sovereign");
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
