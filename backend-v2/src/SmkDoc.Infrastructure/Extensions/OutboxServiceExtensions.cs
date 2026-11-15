using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;
using SmkDoc.Infrastructure.BackgroundWorkers;
using SmkDoc.Infrastructure.Events;
using SmkDoc.Infrastructure.Persistence.Outbox;

namespace SmkDoc.Infrastructure.Extensions;

public static class OutboxServiceExtensions
{
    public static IServiceCollection AddOutboxInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OutboxWorkerOptions>(configuration.GetSection(OutboxWorkerOptions.SectionName));
        services.Configure<IdempotencyCleanupOptions>(configuration.GetSection(IdempotencyCleanupOptions.SectionName));
        services.Configure<DocumentJobWorkerOptions>(configuration.GetSection(DocumentJobWorkerOptions.SectionName));
        services.Configure<DocumentRetentionWorkerOptions>(configuration.GetSection("DocumentRetentionWorker"));

        services.AddScoped<OutboxSaveChangesInterceptor>();

        services.AddScoped<IDomainEventHandler<ProjectActivatedDomainEvent>, TenantCacheInvalidationHandler>();
        services.AddScoped<IDomainEventHandler<ProjectDeactivatedDomainEvent>, TenantCacheInvalidationHandler>();
        services.AddScoped<IDomainEventHandler<BreakGlassGrantedDomainEvent>, TenantCacheInvalidationHandler>();
        services.AddScoped<IDomainEventHandler<BreakGlassRevokedDomainEvent>, TenantCacheInvalidationHandler>();
        services.AddScoped<IDomainEventHandler<UserDeactivatedDomainEvent>, TenantCacheInvalidationHandler>();

        services.AddScoped<IDomainEventHandler<BreakGlassGrantedDomainEvent>, BreakGlassAuditHandler>();
        services.AddScoped<IDomainEventHandler<BreakGlassRevokedDomainEvent>, BreakGlassAuditHandler>();

        services.AddBackgroundWorkers(configuration, forceEnable: false);
        return services;
    }

    public static IServiceCollection AddBackgroundWorkers(
        this IServiceCollection services,
        IConfiguration configuration,
        bool forceEnable = false)
    {
        bool runWorkers = forceEnable || configuration.GetValue("Worker:RunInApiHost", true);
        if (!runWorkers)
        {
            return services;
        }

        services.AddHostedService<OutboxRelayWorker>();
        services.AddHostedService<IdempotencyCleanupWorker>();
        services.AddHostedService<DocumentJobWorker>();
        services.AddHostedService<DocumentRetentionWorker>();
        services.AddHostedService<GenerationLogBatchWorker>();

        return services;
    }
}
