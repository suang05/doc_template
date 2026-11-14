using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Api.Extensions;

public static partial class DatabaseExtensions
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
        catch (System.Data.Common.DbException ex)
        {
            LogDbInitFailed(logger, ex);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            LogDbInitNetworkError(logger, ex);
        }
        catch (TimeoutException ex)
        {
            LogDbInitTimedOut(logger, ex);
        }
        catch (InvalidOperationException ex)
        {
            LogDbInitConfigError(logger, ex);
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
        var uow = services.GetRequiredService<IUnitOfWork>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        var company = await EnsureDefaultCompanyAsync(companyRepo, uow, now, logger);
        var project = await EnsureDefaultProjectAsync(projectRepo, uow, company.Id, now, logger);

        string adminEmailValue = configuration["Admin:Email"]
                                 ?? configuration["ADMIN_EMAIL"]
                                 ?? "admin@sammakorn.co.th";
        string adminPasswordValue = configuration["Admin:Password"]
                                    ?? configuration["ADMIN_PASSWORD"]
                                    ?? "Admin@2026!";

        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        var adminUser = await EnsureSuperAdminUserAsync(
            userRepository, passwordHasher, uow, adminEmailValue, adminPasswordValue, now, logger);

        await EnsureSuperAdminProjectRoleAsync(
            userProjectRoleRepository, uow, project, adminUser, adminEmailValue, now, logger);

        await EnsureMasterApiKeyAsync(apiKeyRepo, uow, configuration, environment, project.Id, now, logger);

        LogSeedSummary(logger, company, project, adminUser);
    }

    private static async Task<Company> EnsureDefaultCompanyAsync(
        ICompanyRepository companyRepo,
        IUnitOfWork uow,
        DateTimeOffset now,
        ILogger logger)
    {
        var company = await companyRepo.GetByNameAsync(CompanyName.Create("SAMMAKORN"), CancellationToken.None);
        if (company == null)
        {
            company = Company.Create(CompanyName.Create("SAMMAKORN"), now);
            await companyRepo.AddAsync(company, CancellationToken.None);
            await uow.CommitAsync(CancellationToken.None);
            LogCreatedDefaultCompany(logger);
            return company;
        }

        if (!company.IsActive)
        {
            company.Activate(now);
            await uow.CommitAsync(CancellationToken.None);
        }

        return company;
    }

    private static async Task<Project> EnsureDefaultProjectAsync(
        IProjectRepository projectRepo,
        IUnitOfWork uow,
        CompanyId companyId,
        DateTimeOffset now,
        ILogger logger)
    {
        var project = await projectRepo.GetFirstForCompanyAsync(companyId, CancellationToken.None);
        if (project == null)
        {
            project = Project.Create(
                companyId,
                ProjectName.Create("Default Project"),
                ProjectSlug.Create("default"),
                now);
            await projectRepo.AddAsync(project, CancellationToken.None);
            await uow.CommitAsync(CancellationToken.None);
            LogCreatedDefaultProject(logger);
            return project;
        }

        if (!project.IsActive)
        {
            project.Activate(now);
            await uow.CommitAsync(CancellationToken.None);
        }

        return project;
    }

    private static async Task<User> EnsureSuperAdminUserAsync(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork uow,
        string adminEmailValue,
        string adminPasswordValue,
        DateTimeOffset now,
        ILogger logger)
    {
        var adminEmail = EmailAddress.Create(adminEmailValue);
        var adminUser = await userRepository.GetByEmailAsync(adminEmail, CancellationToken.None);
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

            await userRepository.AddAsync(adminUser, CancellationToken.None);
            await uow.CommitAsync(CancellationToken.None);
            LogSeededSuperAdmin(logger, adminEmailValue);
            return adminUser;
        }

        bool needsUpdate = SyncSuperAdminProperties(adminUser, passwordHasher, adminEmailValue, adminPasswordValue, now, logger);
        if (needsUpdate)
        {
            await uow.CommitAsync(CancellationToken.None);
        }

        return adminUser;
    }

    private static bool SyncSuperAdminProperties(
        User adminUser,
        IPasswordHasher passwordHasher,
        string adminEmailValue,
        string adminPasswordValue,
        DateTimeOffset now,
        ILogger logger)
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
            LogSynchronizedAdminPassword(logger, adminEmailValue);
        }

        return needsUpdate;
    }

    private static async Task EnsureSuperAdminProjectRoleAsync(
        IUserProjectRoleRepository userProjectRoleRepository,
        IUnitOfWork uow,
        Project project,
        User adminUser,
        string adminEmailValue,
        DateTimeOffset now,
        ILogger logger)
    {
        var existingRole = await userProjectRoleRepository.GetAsync(project.Id, adminUser.Id, CancellationToken.None);
        if (existingRole == null)
        {
            var projectRole = UserProjectRole.Create(adminUser.Id, project.Id, RoleType.Admin, now);
            await userProjectRoleRepository.AddAsync(projectRole, CancellationToken.None);
            await uow.CommitAsync(CancellationToken.None);
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogAssignedAdminRole(logger, adminEmailValue, project.Name.Value);
            }
            return;
        }

        if (existingRole.Role != RoleType.Admin)
        {
            existingRole.UpdateRole(RoleType.Admin);
            await uow.CommitAsync(CancellationToken.None);
        }
    }

    private static async Task EnsureMasterApiKeyAsync(
        IApiKeyRepository apiKeyRepo,
        IUnitOfWork uow,
        IConfiguration configuration,
        IHostEnvironment environment,
        ProjectId projectId,
        DateTimeOffset now,
        ILogger logger)
    {
        string? masterApiKey = configuration["MASTER_API_KEY"] ?? configuration["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(masterApiKey))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY ERROR: MASTER_API_KEY or Security:ApiKey must be set in Production.");
            }

            masterApiKey = ApiKeyGenerator.GenerateSecret("smk_dev_");
            LogGeneratedTempDevApiKey(logger);
        }

        try
        {
            var defaultKeyHash = new Sha256Hash(ApiKeyGenerator.ComputeHash(masterApiKey));
            var existingKey = await apiKeyRepo.GetByKeyHashAsync(defaultKeyHash, CancellationToken.None);
            if (existingKey == null)
            {
                var keyEntity = ApiKey.Issue(
                    projectId,
                    ApiKeyName.Create("Master Environment Key"),
                    "master",
                    defaultKeyHash,
                    ExpirationPolicy.Never,
                    now);

                await apiKeyRepo.AddAsync(keyEntity, CancellationToken.None);
                await uow.CommitAsync(CancellationToken.None);
                LogSeededMasterApiKey(logger);
            }
        }
        catch (System.Data.Common.DbException ex)
        {
            LogMasterApiKeyDbWarning(logger, ex);
        }
        catch (TimeoutException ex)
        {
            LogMasterApiKeyTimeoutWarning(logger, ex);
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            LogMasterApiKeyDomainWarning(logger, ex);
        }
    }

    private static void LogSeedSummary(ILogger logger, Company company, Project project, User adminUser)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogStartupInitializationSummary(
                logger,
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

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Database initialization failed on startup. All database operations will fail.")]
    private static partial void LogDbInitFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Database initialization failed on startup due to network error.")]
    private static partial void LogDbInitNetworkError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Database initialization timed out on startup.")]
    private static partial void LogDbInitTimedOut(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Database initialization configuration error on startup.")]
    private static partial void LogDbInitConfigError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Created default company: SAMMAKORN")]
    private static partial void LogCreatedDefaultCompany(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Created default project: Default Project (default)")]
    private static partial void LogCreatedDefaultProject(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded default Super Admin user: {Email}")]
    private static partial void LogSeededSuperAdmin(ILogger logger, string email);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Synchronized Super Admin password with configured credentials: {Email}")]
    private static partial void LogSynchronizedAdminPassword(ILogger logger, string email);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Assigned Admin role to {Email} in project {ProjectName}")]
    private static partial void LogAssignedAdminRole(ILogger logger, string email, string projectName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Generated temporary Development API Key for local initialization. Set MASTER_API_KEY or Security:ApiKey explicitly if a deterministic key is required.")]
    private static partial void LogGeneratedTempDevApiKey(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded default Master API key.")]
    private static partial void LogSeededMasterApiKey(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Non-fatal database error while seeding Master API Key. Super Admin access remains active.")]
    private static partial void LogMasterApiKeyDbWarning(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Non-fatal timeout while seeding Master API Key. Super Admin access remains active.")]
    private static partial void LogMasterApiKeyTimeoutWarning(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Non-fatal domain error while seeding Master API Key. Super Admin access remains active.")]
    private static partial void LogMasterApiKeyDomainWarning(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "\n======================================================================\n SMK Document Server - Essential Database Initialization Summary\n======================================================================\n  => Company:       {Company} (Active: {CompanyActive})\n  => Project:       {Project} [slug: {Slug}] (Active: {ProjectActive})\n  => Super Admin:   {AdminEmail} (Active: {AdminActive}, Role: {Role})\n  => Project Role:  Admin (Project: {AssignedProject})\n======================================================================")]
    private static partial void LogStartupInitializationSummary(
        ILogger logger,
        string company,
        bool companyActive,
        string project,
        string slug,
        bool projectActive,
        string adminEmail,
        bool adminActive,
        string role,
        string assignedProject);
}
