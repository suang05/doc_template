using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmkDoc.Api.Extensions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Extensions;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Collection(PostgresIntegrationSuiteDefinition.Name)]
public sealed class BootstrapDatabaseSeedingIntegrationTests(PostgreSqlContainerFixture fixture)
{
    private const string TestPepper = "bootstrap-integration-test-pepper-minimum-32-chars!";
    private const string TestEncryptionKey = "bootstrap-integration-encryption-key-min-32-bytes!!";
    private const string TestJwtSecret = "bootstrap-integration-jwt-secret-key-minimum-32-bytes";

    [RequiresPostgreSqlFact]
    public async Task InitializeDatabaseAsync_OnFreshDatabase_SeedsMasterKeyAndSuperAdmin_AndIsIdempotentOnReboot()
    {
        const string masterKey = "smk_live_BootKeyId123_BootstrapMasterSecretToken1234567890";
        const string adminEmail = "bootstrap-admin@sammakorn.co.th";
        const string adminPassword = "BootStr@p2026!";

        string dbName = $"smkdoc_boot_{Guid.NewGuid():N}";
        await ExecuteAdminDatabaseSqlAsync($"CREATE DATABASE \"{dbName}\"");

        try
        {
            string migratorConn = BuildDbConnectionString(dbName, asAppRole: false);
            string appConn = BuildDbConnectionString(dbName, asAppRole: true);

            await using ServiceProvider services = BuildBootstrapServiceProvider(
                migratorConn,
                appConn,
                masterKey,
                adminEmail,
                adminPassword);

            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            ILogger<BootstrapDatabaseSeedingIntegrationTests> logger =
                services.GetRequiredService<ILogger<BootstrapDatabaseSeedingIntegrationTests>>();

            TestHostEnvironment prodEnv = new(Environments.Production);

            // First boot on empty database + idempotent reboot
            await DatabaseExtensions.InitializeDatabaseAsync(services, configuration, prodEnv, logger);
            await DatabaseExtensions.InitializeDatabaseAsync(services, configuration, prodEnv, logger);

            Guid companyId;
            Guid projectId;
            Guid adminUserId;
            string passwordHash;
            Guid apiKeyId;
            byte[]? secretHashBytes;

            await using (NpgsqlConnection conn = new(migratorConn))
            {
                await conn.OpenAsync();

                await using NpgsqlCommand companyCmd = new(
                    "SELECT COUNT(*)::int, MIN(id::text)::uuid FROM companies WHERE name = 'SAMMAKORN' AND is_active = true",
                    conn);
                await using (NpgsqlDataReader r = await companyCmd.ExecuteReaderAsync())
                {
                    (await r.ReadAsync()).Should().BeTrue();
                    r.GetInt32(0).Should().Be(1);
                    companyId = r.GetGuid(1);
                }

                await using NpgsqlCommand projectCmd = new(
                    "SELECT COUNT(*)::int, MIN(id::text)::uuid FROM projects WHERE company_id = @cid AND slug = 'default' AND is_active = true",
                    conn);
                projectCmd.Parameters.AddWithValue("cid", companyId);
                await using (NpgsqlDataReader r = await projectCmd.ExecuteReaderAsync())
                {
                    (await r.ReadAsync()).Should().BeTrue();
                    r.GetInt32(0).Should().Be(1);
                    projectId = r.GetGuid(1);
                }

                await using NpgsqlCommand quotaCmd = new(
                    "SELECT COUNT(*)::int FROM project_quotas WHERE project_id = @pid AND is_active = true",
                    conn);
                quotaCmd.Parameters.AddWithValue("pid", projectId);
                Convert.ToInt32(await quotaCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture).Should().Be(1);

                await using NpgsqlCommand userCmd = new(
                    "SELECT id, password_hash, system_role, is_active FROM users WHERE email = @email",
                    conn);
                userCmd.Parameters.AddWithValue("email", adminEmail);
                await using (NpgsqlDataReader r = await userCmd.ExecuteReaderAsync())
                {
                    (await r.ReadAsync()).Should().BeTrue();
                    adminUserId = r.GetGuid(0);
                    passwordHash = r.GetString(1);
                    r.GetString(2).Should().Be("SuperAdmin");
                    r.GetBoolean(3).Should().BeTrue();
                    (await r.ReadAsync()).Should().BeFalse();
                }

                await using NpgsqlCommand roleCountCmd = new(
                    "SELECT COUNT(*)::int FROM user_project_roles WHERE user_id = @uid AND project_id = @pid AND role = 'Admin'",
                    conn);
                roleCountCmd.Parameters.AddWithValue("uid", adminUserId);
                roleCountCmd.Parameters.AddWithValue("pid", projectId);
                Convert.ToInt32(await roleCountCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture).Should().Be(1);

                await using NpgsqlCommand keyCmd = new(
                    "SELECT id, project_id, name, key_prefix, key_id, key_suffix, secret_hash_bytes, is_active FROM api_keys WHERE name = 'Master Environment Key'",
                    conn);
                await using (NpgsqlDataReader r = await keyCmd.ExecuteReaderAsync())
                {
                    (await r.ReadAsync()).Should().BeTrue();
                    apiKeyId = r.GetGuid(0);
                    r.GetGuid(1).Should().Be(projectId);
                    r.GetString(2).Should().Be("Master Environment Key");
                    r.GetString(3).Should().Be("smk_live");
                    r.GetString(4).Should().Be("BootKeyId123");
                    r.GetString(5).Should().Be("7890");
                    secretHashBytes = await r.IsDBNullAsync(6) ? null : await r.GetFieldValueAsync<byte[]>(6);
                    r.GetBoolean(7).Should().BeTrue();
                    (await r.ReadAsync()).Should().BeFalse();
                }
            }

            secretHashBytes.Should().NotBeNull();
            secretHashBytes!.Length.Should().Be(32);

            IPasswordHasher passwordHasher = services.GetRequiredService<IPasswordHasher>();
            passwordHasher.VerifyPassword(adminPassword, passwordHash).Should().BeTrue();

            // Verify runtime smk_app auth functions & IApiKeyVerifier succeed without superuser privileges
            await using (NpgsqlConnection appConnHandle = new(appConn))
            {
                await appConnHandle.OpenAsync();
                await using NpgsqlCommand authRoleCmd = new(
                    "SELECT project_id, user_id, role FROM fn_resolve_user_project_role_for_auth(@pid, @uid)",
                    appConnHandle);
                authRoleCmd.Parameters.AddWithValue("pid", projectId);
                authRoleCmd.Parameters.AddWithValue("uid", adminUserId);
                await using NpgsqlDataReader r = await authRoleCmd.ExecuteReaderAsync();
                (await r.ReadAsync()).Should().BeTrue();
                r.GetGuid(0).Should().Be(projectId);
                r.GetGuid(1).Should().Be(adminUserId);
                r.GetString(2).Should().Be("Admin");
            }

            IApiKeyVerifier apiKeyVerifier = services.GetRequiredService<IApiKeyVerifier>();
            VerifiedApiKeyResult? verified = await apiKeyVerifier.VerifyAsync(masterKey, CancellationToken.None);
            verified.Should().NotBeNull();
            verified!.KeyId.Value.Should().Be(apiKeyId);
            verified.ProjectId.Value.Should().Be(projectId);
            verified.IsLegacySha256.Should().BeFalse();

            // Verify EF Core materializes non-expiring API keys (expires_at IS NULL) without NullReferenceException
            await using (AsyncServiceScope verifyScope = services.CreateAsyncScope())
            {
                ICurrentProjectContextSetter setter =
                    verifyScope.ServiceProvider.GetRequiredService<ICurrentProjectContextSetter>();
                setter.SetProject(new ProjectId(projectId), "bootstrap-verify");

                AppDbContext verifyDb =
                    verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
                DateTimeOffset now = services.GetRequiredService<TimeProvider>().GetUtcNow();
                List<ApiKey> materializedKeys = await verifyDb.ApiKeys
                    .AsNoTracking()
                    .ToListAsync();

                materializedKeys.Should().ContainSingle();
                materializedKeys[0].Expiration.Should().Be(ExpirationPolicy.Never);
                materializedKeys[0].IsExpiredAt(now).Should().BeFalse();
                materializedKeys[0].IsUsableAt(now).Should().BeTrue();
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAdminDatabaseSqlAsync($"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)");
        }
    }

    [Fact]
    public void ValidateMasterApiKeyConfiguration_InProductionWithLegacyKeyAndLegacyDisabled_ThrowsInvalidOperationException()
    {
        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            ["Security:ApiKeyPepper"] = TestPepper,
            ["Security:AllowLegacySha256Keys"] = "false",
            ["MASTER_API_KEY"] = "secret-smk-key-2026"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        ServiceCollection services = new();
        services.AddSecurityInfrastructure(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        Action act = () => DatabaseExtensions.ValidateMasterApiKeyConfiguration(
            provider,
            configuration,
            new TestHostEnvironment(Environments.Production));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*smk_live_*AllowLegacySha256Keys*");
    }

    [RequiresPostgreSqlFact]
    public async Task InitializeDatabaseAsync_WhenMasterKeyMissing_FailsFastInProduction_AndAutoMintsInDevelopment()
    {
        string dbName = $"smkdoc_devboot_{Guid.NewGuid():N}";
        await ExecuteAdminDatabaseSqlAsync($"CREATE DATABASE \"{dbName}\"");

        try
        {
            string migratorConn = BuildDbConnectionString(dbName, asAppRole: false);
            string appConn = BuildDbConnectionString(dbName, asAppRole: true);

            await using ServiceProvider services = BuildBootstrapServiceProvider(
                migratorConn,
                appConn,
                masterApiKey: null,
                adminEmail: null,
                adminPassword: null);

            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            ILogger<BootstrapDatabaseSeedingIntegrationTests> logger =
                services.GetRequiredService<ILogger<BootstrapDatabaseSeedingIntegrationTests>>();

            // 1. Production mode without MASTER_API_KEY must fail fast
            Func<Task> prodAttempt = () => DatabaseExtensions.InitializeDatabaseAsync(
                services,
                configuration,
                new TestHostEnvironment(Environments.Production),
                logger);

            await prodAttempt.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*MASTER_API_KEY*");

            // 2. Development first boot & reboot auto-mints ephemeral structured key idempotently
            TestHostEnvironment devEnv = new(Environments.Development);
            await DatabaseExtensions.InitializeDatabaseAsync(services, configuration, devEnv, logger);
            await DatabaseExtensions.InitializeDatabaseAsync(services, configuration, devEnv, logger);

            await using NpgsqlConnection conn = new(migratorConn);
            await conn.OpenAsync();

            await using NpgsqlCommand companyCount = new("SELECT COUNT(*)::int FROM companies", conn);
            Convert.ToInt32(await companyCount.ExecuteScalarAsync(), CultureInfo.InvariantCulture).Should().Be(1);

            await using NpgsqlCommand projectCount = new("SELECT COUNT(*)::int FROM projects", conn);
            Convert.ToInt32(await projectCount.ExecuteScalarAsync(), CultureInfo.InvariantCulture).Should().Be(1);

            await using NpgsqlCommand quotaCount = new("SELECT COUNT(*)::int FROM project_quotas", conn);
            Convert.ToInt32(await quotaCount.ExecuteScalarAsync(), CultureInfo.InvariantCulture).Should().Be(1);

            await using NpgsqlCommand adminCmd = new(
                "SELECT password_hash, system_role FROM users WHERE email = 'admin@sammakorn.co.th'",
                conn);
            string? defaultHash = null;
            await using (NpgsqlDataReader r = await adminCmd.ExecuteReaderAsync())
            {
                (await r.ReadAsync()).Should().BeTrue();
                defaultHash = r.GetString(0);
                r.GetString(1).Should().Be("SuperAdmin");
                (await r.ReadAsync()).Should().BeFalse();
            }

            IPasswordHasher passwordHasher = services.GetRequiredService<IPasswordHasher>();
            passwordHasher.VerifyPassword("Admin@2026!", defaultHash!).Should().BeTrue();

            await using NpgsqlCommand keyCmd = new(
                "SELECT COUNT(*)::int, MIN(key_prefix), MIN(key_id), MIN(octet_length(secret_hash_bytes)) FROM api_keys WHERE is_active = true",
                conn);
            await using (NpgsqlDataReader r = await keyCmd.ExecuteReaderAsync())
            {
                (await r.ReadAsync()).Should().BeTrue();
                r.GetInt32(0).Should().Be(1);
                r.GetString(1).Should().Be("smk_live");
                (await r.IsDBNullAsync(2)).Should().BeFalse();
                r.GetString(2).Length.Should().Be(12);
                r.GetInt32(3).Should().Be(32);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAdminDatabaseSqlAsync($"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)");
        }
    }

    private static ServiceProvider BuildBootstrapServiceProvider(
        string migratorConnectionString,
        string appConnectionString,
        string? masterApiKey,
        string? adminEmail,
        string? adminPassword)
    {
        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            ["ConnectionStrings:MigratorConnection"] = migratorConnectionString,
            ["ConnectionStrings:DefaultConnection"] = appConnectionString,
            ["Security:ApiKeyPepper"] = TestPepper,
            ["EncryptionKey"] = TestEncryptionKey,
            ["Jwt:Secret"] = TestJwtSecret,
            ["Worker:RunInApiHost"] = "false"
        };

        if (masterApiKey is not null)
        {
            settings["MASTER_API_KEY"] = masterApiKey;
        }

        if (adminEmail is not null)
        {
            settings["Admin:Email"] = adminEmail;
        }

        if (adminPassword is not null)
        {
            settings["Admin:Password"] = adminPassword;
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSecurityInfrastructure(configuration);
        services.AddPersistenceInfrastructure(configuration);
        services.AddOutboxInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private string BuildDbConnectionString(string databaseName, bool asAppRole)
    {
        NpgsqlConnectionStringBuilder builder = new(fixture.ConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        if (asAppRole)
        {
            builder.Username = PostgreSqlContainerFixture.AppRoleUsername;
            builder.Password = PostgreSqlContainerFixture.AppRolePassword;
        }

        return builder.ConnectionString;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Database name is strictly constructed internally from a literal prefix and Guid.NewGuid().ToString(\"N\").")]
    private async Task ExecuteAdminDatabaseSqlAsync(string sql)
    {
        NpgsqlConnectionStringBuilder adminBuilder = new(fixture.ConnectionString)
        {
            Pooling = false
        };

        await using NpgsqlConnection conn = new(adminBuilder.ConnectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SmkDoc.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
