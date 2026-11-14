using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Data;
using SmkDoc.Infrastructure.Data.Drivers;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Infrastructure.Extensions;

public static class SecurityInfrastructureExtensions
{
    public static IServiceCollection AddSecurityInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDataProtectionService, DataProtectionService>();
        services.AddSingleton<IDocxSecurityScanner, DocxSecurityScannerService>();
        services.AddSingleton<ISqlConnectionValidator, SqlSecurityGuard>();
        services.AddSingleton<ISqlDriverStrategy, PostgreSqlDriverStrategy>();
        services.AddSingleton<ISqlDriverFactory, SqlDriverFactory>();
        services.AddScoped<ISqlExecutorService, SqlExecutorService>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ISecurityAuditService, SecurityAuditService>();

        return services;
    }
}
