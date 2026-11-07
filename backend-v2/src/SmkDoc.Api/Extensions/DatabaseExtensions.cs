using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
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

            await SeedEssentialDataAsync(services, app.Configuration, app.Environment, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed on startup. All database operations will fail.");
        }
    }

    private static async Task SeedEssentialDataAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var companyRepo = services.GetRequiredService<ICompanyRepository>();
        var projectRepo = services.GetRequiredService<IProjectRepository>();
        var userRepository = services.GetRequiredService<IUserRepository>();
        var userProjectRoleRepository = services.GetRequiredService<IUserProjectRoleRepository>();
        var apiKeyRepo = services.GetRequiredService<IApiKeyRepository>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        var uow = services.GetRequiredService<IUnitOfWork>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        // 1. Ensure Default Company
        var company = await companyRepo.GetByNameAsync(CompanyName.Create("SAMMAKORN"));
        if (company == null)
        {
            company = Company.Create(CompanyName.Create("SAMMAKORN"), now);
            await companyRepo.AddAsync(company);
            await uow.CommitAsync();
            logger.LogInformation("Created default company: SAMMAKORN");
        }
        else if (!company.IsActive)
        {
            company.Activate(now);
            await uow.CommitAsync();
        }

        // 2. Ensure Default Project
        var project = await projectRepo.GetFirstForCompanyAsync(company.Id);
        if (project == null)
        {
            project = Project.Create(
                company.Id,
                ProjectName.Create("Default Project"),
                ProjectSlug.Create("default"),
                now);
            await projectRepo.AddAsync(project);
            await uow.CommitAsync();
            logger.LogInformation("Created default project: Default Project (default)");
        }
        else if (!project.IsActive)
        {
            project.Activate(now);
            await uow.CommitAsync();
        }

        // 3. Ensure Super Admin User (Guaranteed Portal Access)
        string adminEmailValue = configuration["Admin:Email"]
                                 ?? configuration["ADMIN_EMAIL"]
                                 ?? "admin@sammakorn.co.th";
        string adminPasswordValue = configuration["Admin:Password"]
                                    ?? configuration["ADMIN_PASSWORD"]
                                    ?? "Admin@2026!";

        var adminEmail = EmailAddress.Create(adminEmailValue);
        var adminUser = await userRepository.GetByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            var passwordHash = passwordHasher.HashPassword(adminPasswordValue);
            adminUser = User.Register(
                adminEmail,
                passwordHash,
                "Admin",
                "User",
                now,
                SystemRole.SuperAdmin);

            await userRepository.AddAsync(adminUser);
            await uow.CommitAsync();
            logger.LogInformation("Seeded default Super Admin user: {Email}", adminEmailValue);
        }
        else
        {
            bool needsUpdate = false;
            if (!adminUser.IsActive)
            {
                adminUser.Activate(now);
                needsUpdate = true;
            }

            if (adminUser.SystemRole != SystemRole.SuperAdmin)
            {
                adminUser.AssignSystemRole(SystemRole.SuperAdmin, now);
                needsUpdate = true;
            }

            if (!passwordHasher.VerifyPassword(adminPasswordValue, adminUser.PasswordHash))
            {
                var newPasswordHash = passwordHasher.HashPassword(adminPasswordValue);
                adminUser.UpdatePassword(newPasswordHash, now);
                needsUpdate = true;
                logger.LogInformation("Synchronized Super Admin password with configured credentials: {Email}",
                    adminEmailValue);
            }

            if (needsUpdate)
            {
                await uow.CommitAsync();
            }
        }

        // 4. Ensure Super Admin Project Role
        var existingRole = await userProjectRoleRepository.GetAsync(project.Id, adminUser.Id);
        if (existingRole == null)
        {
            var projectRole = UserProjectRole.Create(adminUser.Id, project.Id, RoleType.Admin, now);
            await userProjectRoleRepository.AddAsync(projectRole);
            await uow.CommitAsync();
            logger.LogInformation("Assigned Admin role to {Email} in project {ProjectName}", adminEmailValue,
                project.Name.Value);
        }
        else if (existingRole.Role != RoleType.Admin)
        {
            existingRole.UpdateRole(RoleType.Admin);
            await uow.CommitAsync();
        }

        // 5. Ensure Master API Key (Fault-Tolerant)
        try
        {
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

            var defaultKeyHash = new Sha256Hash(ApiKeyGenerator.ComputeHash(masterApiKey));
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
                logger.LogInformation("Seeded default Master API key.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Non-fatal error while seeding Master API Key. Super Admin access remains active.");
        }

        // 6. Startup Diagnostic Verification Output
        logger.LogInformation(
            "\n======================================================================\n" +
            " SMK Document Server - Essential Database Initialization Summary\n" +
            "======================================================================\n" +
            "  => Company:       {Company} (Active: {CompanyActive})\n" +
            "  => Project:       {Project} [slug: {Slug}] (Active: {ProjectActive})\n" +
            "  => Super Admin:   {AdminEmail} (Active: {AdminActive}, Role: {Role})\n" +
            "  => Project Role:  Admin (Project: {AssignedProject})\n" +
            "======================================================================",
            company.Name.Value,
            company.IsActive,
            project.Name.Value,
            project.Slug.Value,
            project.IsActive,
            adminUser.Email.Value,
            adminUser.IsActive,
            adminUser.SystemRole.Name,
            project.Name.Value);
    }
}
