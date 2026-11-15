using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Data;
using SmkDoc.Infrastructure.Data.Drivers;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Infrastructure.Extensions;

public static class SecurityInfrastructureExtensions
{
    public static IServiceCollection AddSecurityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));
        services.Configure<EgressPolicyOptions>(configuration.GetSection(EgressPolicyOptions.SectionName));

        services.AddSingleton<IDataProtectionService, DataProtectionService>();
        services.AddSingleton<IDocxSecurityScanner, DocxSecurityScannerService>();

        services.AddScoped<IConnectionHostValidator>(sp => new ConnectionHostValidator(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IOptions<EgressPolicyOptions>>(),
            sp.GetService<IProjectStatusReader>(),
            sp.GetRequiredService<ISecurityAuditService>()));

        services.AddSingleton<ISqlDriverStrategy, PostgreSqlDriverStrategy>();
        services.AddSingleton<ISqlDriverFactory, SqlDriverFactory>();
        services.AddScoped<ISqlExecutorService, SqlExecutorService>();

        services.AddSingleton<IApiKeyCredentialGenerator, ApiKeyCredentialGenerator>();
        services.AddSingleton<IApiKeyVerifier>(sp => new ApiKeyVerifier(
            sp.GetRequiredService<IOptions<ApiKeyOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<NpgsqlDataSource>(),
            sp.GetService<IServiceScopeFactory>()));

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ISecurityAuditService, SecurityAuditService>();

        return services;
    }
}
