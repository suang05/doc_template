using SmkDoc.Api.Extensions;
using SmkDoc.Application;
using SmkDoc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Host Port & Binding Configuration
builder.ConfigureHostPort();

// 2. Clean Architecture Layer Dependency Inversion
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddPresentationServices(builder.Configuration);

var app = builder.Build();

// 3. Database Initialization & Startup Seeding (Scoped Lifecycle)
await app.ApplyMigrationsAndSeedAsync();

// 4. HTTP Request Middleware Pipeline & Endpoint Topology
app.UsePresentationPipeline();

app.Run();

public partial class Program { }
