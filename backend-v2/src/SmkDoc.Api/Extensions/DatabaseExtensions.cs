using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            await SeedDefaultDataAsync(services, app.Configuration, app.Environment, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed on startup. All database operations will fail.");
        }
    }

    private static async Task SeedDefaultDataAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var companyRepo = services.GetRequiredService<IRepository<Company>>();
        var projectRepo = services.GetRequiredService<IRepository<Project>>();
        var apiKeyRepo = services.GetRequiredService<IApiKeyRepository>();
        var uow = services.GetRequiredService<IUnitOfWork>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        var company = await companyRepo.FirstOrDefaultAsync(c => c.Name == CompanyName.Create("SAMMAKORN"));
        if (company == null)
        {
            company = Company.Create(CompanyName.Create("SAMMAKORN"), now);
            await companyRepo.AddAsync(company);
            await uow.CommitAsync();
        }

        var project = await projectRepo.FirstOrDefaultAsync(p => p.CompanyId == company.Id);
        if (project == null)
        {
            project = Project.Create(
                company.Id,
                ProjectName.Create("Default Project"),
                ProjectSlug.Create("default"),
                now);
            await projectRepo.AddAsync(project);
            await uow.CommitAsync();
        }

        string? masterApiKey = configuration["MASTER_API_KEY"] ?? configuration["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(masterApiKey))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY ERROR: MASTER_API_KEY or Security:ApiKey must be set in Production.");
            }

            masterApiKey = "dev-smk-key-2026";
            logger.LogWarning("Using temporary Development API Key. NEVER use this in production.");
        }

        var defaultKeyHash = new Sha256Hash(ApiKeyHelper.ComputeHash(masterApiKey));
        var existingKey = await apiKeyRepo.GetByKeyHashAsync(defaultKeyHash);
        if (existingKey == null)
        {
            var keyEntity = ApiKey.Issue(
                project.Id,
                ApiKeyName.Create("Master Environment Key"),
                "master",
                defaultKeyHash,
                ExpirationPolicy.Never,
                now);

            await apiKeyRepo.AddAsync(keyEntity);
            await uow.CommitAsync();
        }
    }
}
