using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Events;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Interceptors;
using SmkDoc.Infrastructure.Persistence.Outbox;
using SmkDoc.Infrastructure.Persistence.Queries;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.Infrastructure.Services.Logging;
using StackExchange.Redis;

namespace SmkDoc.Infrastructure.Extensions;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Ambient Project & Execution Scopes
        services.AddMemoryCache();
        services.AddScoped<CurrentProjectContext>();
        services.AddScoped<ICurrentProjectContext>(sp => sp.GetRequiredService<CurrentProjectContext>());
        services.AddScoped<ICurrentProjectContextSetter>(sp => sp.GetRequiredService<CurrentProjectContext>());
        services.AddScoped<ITenantScope, TenantScope>();
        services.AddScoped<IIdempotencyExecutionScope, IdempotencyExecutionScope>();

        // 2. Database, NpgsqlDataSource Pool & EF Core RLS / Outbox Interceptors
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException(
                "Database connection string is required. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());

        services.AddScoped<TenantDbConnectionInterceptor>();
        services.AddScoped<TenantDbTransactionInterceptor>();
        services.AddScoped<TenantSecuritySaveChangesInterceptor>();
        services.AddScoped<OutboxSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            options.UsePostgreSqlConventions(dataSource, sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
            options.AddInterceptors(
                sp.GetRequiredService<TenantDbConnectionInterceptor>(),
                sp.GetRequiredService<TenantDbTransactionInterceptor>(),
                sp.GetRequiredService<TenantSecuritySaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        // 3. Repositories
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IDataConnectionRepository, DataConnectionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserProjectRoleRepository, UserProjectRoleRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IGenerationLogRepository, GenerationLogRepository>();
        services.AddScoped<IGenerationLogMetricsRepository, GenerationLogMetricsRepository>();
        services.AddScoped<IProjectQuotaRepository, ProjectQuotaRepository>();
        services.AddScoped<IFontRepository, FontRepository>();
        services.AddScoped<IBreakGlassGrantRepository, BreakGlassGrantRepository>();
        services.AddScoped<IDocumentJobRepository, DocumentJobRepository>();

        // 4. Query Services, OHS Readers, Perimeter Resolver & Units of Work
        services.AddScoped<IGenerationLogQueryService, GenerationLogQueryService>();
        services.AddSingleton<IGenerationLogQueue, ChannelGenerationLogQueue>();
        services.AddScoped<IUserWorkspaceQueryService, UserWorkspaceQueryService>();
        services.AddScoped<IProjectQueryService, ProjectQueryService>();
        services.AddScoped<ITemplateQueryService, TemplateQueryService>();

        services.AddScoped<IProjectStatusReader, ProjectStatusReader>();
        services.AddScoped<IQuotaReader, QuotaReader>();
        services.AddScoped<IFontReadModel, FontReadModel>();
        services.AddScoped<IBreakGlassGrantReader, BreakGlassGrantReader>();
        services.AddScoped<IDocumentJobReadModel, DocumentJobReadModel>();

        services.AddSingleton(sp => new TenantResolutionResolver(
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<ILogger<TenantResolutionResolver>>(),
            sp.GetService<NpgsqlDataSource>(),
            sp.GetService<IServiceScopeFactory>(),
            sp.GetService<IConnectionMultiplexer>()));
        services.AddSingleton<ITenantResolutionResolver>(sp => sp.GetRequiredService<TenantResolutionResolver>());
        services.AddSingleton<ITenantResolutionPort>(sp => sp.GetRequiredService<TenantResolutionResolver>());

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
