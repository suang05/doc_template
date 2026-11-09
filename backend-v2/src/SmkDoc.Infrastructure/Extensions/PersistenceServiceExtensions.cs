using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Infrastructure.BackgroundWorkers;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Events;
using SmkDoc.Infrastructure.Observability;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Interceptors;
using SmkDoc.Infrastructure.Persistence.Queries;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.Infrastructure.Services.Logging;

namespace SmkDoc.Infrastructure.Extensions;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Ambient Project & Concurrency Context
        services.AddScoped<CurrentProjectContext>();
        services.AddScoped<ICurrentProjectContext>(sp => sp.GetRequiredService<CurrentProjectContext>());
        services.AddScoped<ICurrentProjectContextSetter>(sp => sp.GetRequiredService<CurrentProjectContext>());
        services.AddScoped<IConcurrencyContext, ConcurrencyContext>();

        // 2. Database & EF Core
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException(
                "Database connection string is required. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

        services.AddScoped<TenantSecuritySaveChangesInterceptor>();
        services.AddScoped<ConcurrencySaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
            options.AddInterceptors(
                sp.GetRequiredService<TenantSecuritySaveChangesInterceptor>(),
                sp.GetRequiredService<ConcurrencySaveChangesInterceptor>());
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

        // 4. Query Services & Units of Work
        services.AddScoped<IGenerationLogQueryService, GenerationLogQueryService>();
        services.AddSingleton<IGenerationLogQueue, ChannelGenerationLogQueue>();
        services.AddHostedService<GenerationLogBatchWorker>();
        services.AddScoped<IUserWorkspaceQueryService, UserWorkspaceQueryService>();
        services.AddScoped<IProjectQueryService, ProjectQueryService>();
        services.AddScoped<ITemplateQueryService, TemplateQueryService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}

