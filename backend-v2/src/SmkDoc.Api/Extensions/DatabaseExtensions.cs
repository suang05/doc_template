using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Api.Extensions;

public static partial class DatabaseExtensions
{
    public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            await InitializeDatabaseAsync(app.Services, app.Configuration, app.Environment, logger);
        }
        catch (System.Data.Common.DbException ex)
        {
            LogDbInitFailed(logger, ex);
            throw;
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            LogDbInitNetworkError(logger, ex);
            throw;
        }
        catch (TimeoutException ex)
        {
            LogDbInitTimedOut(logger, ex);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            LogDbInitConfigError(logger, ex);
            throw;
        }
    }

    internal static async Task InitializeDatabaseAsync(
        IServiceProvider rootServices,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        string? migratorConnectionString = configuration.GetConnectionString("MigratorConnection");
        string? defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"];

        if (!string.IsNullOrWhiteSpace(migratorConnectionString))
        {
            var migratorOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UsePostgreSqlConventions(migratorConnectionString, sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null))
                .Options;

            await using (var migratorDb = new AppDbContext(migratorOptions))
            {
                await migratorDb.Database.MigrateAsync();
            }

            await SynchronizeAppRolePasswordAsync(migratorConnectionString, defaultConnectionString, logger);
        }
        else
        {
            using var migrationScope = rootServices.CreateScope();
            var db = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        await SeedEssentialDataAsync(rootServices, configuration, environment, logger);
    }

    private static async Task SynchronizeAppRolePasswordAsync(
        string migratorConnectionString,
        string? defaultConnectionString,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(defaultConnectionString))
        {
            return;
        }

        var defaultBuilder = new NpgsqlConnectionStringBuilder(defaultConnectionString);
        if (string.IsNullOrWhiteSpace(defaultBuilder.Username) ||
            string.IsNullOrWhiteSpace(defaultBuilder.Password) ||
            string.Equals(defaultBuilder.Username, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var conn = new NpgsqlConnection(migratorConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT set_config('app.bootstrap_role', @p_role, false);
            SELECT set_config('app.bootstrap_secret', @p_secret, false);
            DO $$
            DECLARE
                v_role text := current_setting('app.bootstrap_role', true);
                v_secret text := current_setting('app.bootstrap_secret', true);
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_role) THEN
                    EXECUTE format('ALTER ROLE %I WITH PASSWORD %L', v_role, v_secret);
                END IF;
            END
            $$;
            SELECT set_config('app.bootstrap_role', '', false);
            SELECT set_config('app.bootstrap_secret', '', false);
            """;

        cmd.Parameters.Add(new NpgsqlParameter("p_role", NpgsqlDbType.Text) { Value = defaultBuilder.Username });
        cmd.Parameters.Add(new NpgsqlParameter("p_secret", NpgsqlDbType.Text) { Value = defaultBuilder.Password });
        await cmd.ExecuteNonQueryAsync();
        LogSynchronizedAppRolePassword(logger, defaultBuilder.Username);
    }

    internal static async Task SeedEssentialDataAsync(
        IServiceProvider rootServices,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        string adminEmailValue = configuration["Admin:Email"]
                                 ?? configuration["ADMIN_EMAIL"]
                                 ?? "admin@sammakorn.co.th";
        string adminPasswordValue = configuration["Admin:Password"]
                                    ?? configuration["ADMIN_PASSWORD"]
                                    ?? "Admin@2026!";

        var now = rootServices.GetRequiredService<TimeProvider>().GetUtcNow();

        Company company;
        Project project;
        User adminUser;

        await using (var controlPlaneScope = rootServices.CreateAsyncScope())
        {
            var cpServices = controlPlaneScope.ServiceProvider;
            var cpSetter = cpServices.GetRequiredService<ICurrentProjectContextSetter>();
            cpSetter.SetControlPlane("bootstrap");

            var companyRepo = cpServices.GetRequiredService<ICompanyRepository>();
            var projectRepo = cpServices.GetRequiredService<IProjectRepository>();
            var userRepository = cpServices.GetRequiredService<IUserRepository>();
            var passwordHasher = cpServices.GetRequiredService<IPasswordHasher>();
            var cpUow = cpServices.GetRequiredService<IUnitOfWork>();

            company = await EnsureDefaultCompanyAsync(companyRepo, cpUow, now, logger);
            project = await EnsureDefaultProjectAsync(projectRepo, cpUow, company.Id, now, logger);
            adminUser = await EnsureSuperAdminUserAsync(
                userRepository, passwordHasher, cpUow, adminEmailValue, adminPasswordValue, now, logger);
        }

        await using (var tenantScope = rootServices.CreateAsyncScope())
        {
            var tenantServices = tenantScope.ServiceProvider;
            var tenantSetter = tenantServices.GetRequiredService<ICurrentProjectContextSetter>();
            tenantSetter.SetProject(project.Id, "bootstrap");

            var projectQuotaRepo = tenantServices.GetRequiredService<IProjectQuotaRepository>();
            var userProjectRoleRepository = tenantServices.GetRequiredService<IUserProjectRoleRepository>();
            var apiKeyRepo = tenantServices.GetRequiredService<IApiKeyRepository>();
            var credentialGenerator = tenantServices.GetService<IApiKeyCredentialGenerator>();
            var tenantUow = tenantServices.GetRequiredService<IUnitOfWork>();

            await EnsureDefaultProjectQuotaAsync(projectQuotaRepo, tenantUow, project.Id, now, logger);

            await EnsureSuperAdminProjectRoleAsync(
                userProjectRoleRepository, tenantUow, project, adminUser, adminEmailValue, now, logger);

            await EnsureMasterApiKeyAsync(
                apiKeyRepo, credentialGenerator, tenantUow, configuration, environment, project.Id, now, logger);
        }

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

    private static async Task EnsureDefaultProjectQuotaAsync(
        IProjectQuotaRepository projectQuotaRepo,
        IUnitOfWork uow,
        ProjectId projectId,
        DateTimeOffset now,
        ILogger logger)
    {
        var existingQuota = await projectQuotaRepo.GetByProjectIdAsync(projectId, CancellationToken.None);
        if (existingQuota is not null)
        {
            return;
        }

        var defaultQuota = ProjectQuota.CreateDefault(projectId, now);
        await projectQuotaRepo.AddAsync(defaultQuota, CancellationToken.None);
        await uow.CommitAsync(CancellationToken.None);
        LogCreatedDefaultProjectQuota(logger, projectId.Value);
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
            existingRole.UpdateRole(RoleType.Admin, now);
            await uow.CommitAsync(CancellationToken.None);
        }
    }

    private static async Task EnsureMasterApiKeyAsync(
        IApiKeyRepository apiKeyRepo,
        IApiKeyCredentialGenerator? credentialGenerator,
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

            await EnsureDevEphemeralMasterApiKeyAsync(
                apiKeyRepo, credentialGenerator, uow, projectId, now, logger);
            return;
        }

        string trimmedMasterKey = masterApiKey.Trim();
        if (credentialGenerator is not null &&
            credentialGenerator.TryParseAndHashStructuredKey(trimmedMasterKey, out var parsedCredential) &&
            parsedCredential is not null)
        {
            await EnsureStructuredMasterApiKeyAsync(apiKeyRepo, uow, projectId, parsedCredential, now, logger);
            return;
        }

        await EnsureLegacyMasterApiKeyAsync(apiKeyRepo, uow, projectId, trimmedMasterKey, now, logger);
    }

    private static async Task EnsureDevEphemeralMasterApiKeyAsync(
        IApiKeyRepository apiKeyRepo,
        IApiKeyCredentialGenerator? credentialGenerator,
        IUnitOfWork uow,
        ProjectId projectId,
        DateTimeOffset now,
        ILogger logger)
    {
        var existingProjectKeys = await apiKeyRepo.ListByProjectAsync(projectId, CancellationToken.None);
        if (existingProjectKeys.Any(k => k.IsActive && string.Equals(k.CallerApp, "master", StringComparison.Ordinal)))
        {
            return;
        }

        if (credentialGenerator is not null)
        {
            var minted = credentialGenerator.MintLiveKey();
            await PersistStructuredMasterKeyAsync(apiKeyRepo, uow, projectId, minted, now);
            LogGeneratedTempDevApiKey(logger);
            return;
        }

        string devMasterKey = ApiKeyGenerator.GenerateSecret("smk_dev_");
        LogGeneratedTempDevApiKey(logger);
        await EnsureLegacyMasterApiKeyAsync(apiKeyRepo, uow, projectId, devMasterKey, now, logger);
    }

    private static async Task EnsureStructuredMasterApiKeyAsync(
        IApiKeyRepository apiKeyRepo,
        IUnitOfWork uow,
        ProjectId projectId,
        MintedApiKeyCredential parsedCredential,
        DateTimeOffset now,
        ILogger logger)
    {
        var projectKeys = await apiKeyRepo.ListByProjectAsync(projectId, CancellationToken.None);
        var existingByKeyId = projectKeys.FirstOrDefault(
            k => string.Equals(k.KeyId, parsedCredential.KeyId, StringComparison.Ordinal));

        if (existingByKeyId is null)
        {
            await PersistStructuredMasterKeyAsync(apiKeyRepo, uow, projectId, parsedCredential, now);
            LogSeededMasterApiKey(logger);
            return;
        }

        if (existingByKeyId.IsActive &&
            existingByKeyId.SecretHash is { } existingHash &&
            CryptographicOperations.FixedTimeEquals(existingHash.Span, parsedCredential.SecretHash.Span))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Master API key with KeyId '{parsedCredential.KeyId}' already exists in a revoked or conflicting state.");
    }

    private static async Task PersistStructuredMasterKeyAsync(
        IApiKeyRepository apiKeyRepo,
        IUnitOfWork uow,
        ProjectId projectId,
        MintedApiKeyCredential credential,
        DateTimeOffset now)
    {
        var structuredMasterKey = ApiKey.IssueStructured(
            projectId,
            ApiKeyName.Create("Master Environment Key"),
            "master",
            credential.KeyPrefix,
            credential.KeyId,
            credential.KeySuffix,
            credential.SecretHash.ToArray(),
            ExpirationPolicy.Never,
            now);

        await apiKeyRepo.AddAsync(structuredMasterKey, CancellationToken.None);
        await uow.CommitAsync(CancellationToken.None);
    }

    private static async Task EnsureLegacyMasterApiKeyAsync(
        IApiKeyRepository apiKeyRepo,
        IUnitOfWork uow,
        ProjectId projectId,
        string rawMasterKey,
        DateTimeOffset now,
        ILogger logger)
    {
        var defaultKeyHash = new Sha256Hash(ApiKeyGenerator.ComputeHash(rawMasterKey));
        var existingKey = await apiKeyRepo.GetByKeyHashAsync(defaultKeyHash, CancellationToken.None);
        if (existingKey is null)
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
        Message = "Synchronized database runtime role credentials for {RoleName}")]
    private static partial void LogSynchronizedAppRolePassword(ILogger logger, string roleName);

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
        Message = "Created default quota configuration for project {ProjectId}")]
    private static partial void LogCreatedDefaultProjectQuota(ILogger logger, Guid projectId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded default Master API key.")]
    private static partial void LogSeededMasterApiKey(ILogger logger);

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
