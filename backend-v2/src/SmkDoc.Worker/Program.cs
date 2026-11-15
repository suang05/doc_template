using Microsoft.Extensions.Hosting;
using SmkDoc.Application;
using SmkDoc.Infrastructure;
using SmkDoc.Infrastructure.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddBackgroundWorkers(builder.Configuration, forceEnable: true);

var host = builder.Build();
await host.RunAsync();
