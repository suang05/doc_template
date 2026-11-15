using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Persistence.Idempotency;
using SmkDoc.Infrastructure.Persistence.Outbox;

namespace SmkDoc.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentProjectContext? _projectContext;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentProjectContext? projectContext = null) :
        base(options)
    {
        _projectContext = projectContext;
    }

    public ProjectId CurrentTenantId => _projectContext?.TryGetProjectId() ?? ProjectId.Empty;
    public bool IsTenantResolved => _projectContext?.IsResolved ?? false;
    public bool IsControlPlane => _projectContext?.IsControlPlane ?? false;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProjectRole> UserProjectRoles => Set<UserProjectRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<FieldMapping> FieldMappings => Set<FieldMapping>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<GenerationLog> GenerationLogs => Set<GenerationLog>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DataConnection> DataConnections => Set<DataConnection>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<TemplateDataset> TemplateDatasets => Set<TemplateDataset>();

    public DbSet<ProjectQuota> ProjectQuotas => Set<ProjectQuota>();
    public DbSet<Font> Fonts => Set<Font>();
    public DbSet<BreakGlassGrant> BreakGlassGrants => Set<BreakGlassGrant>();
    public DbSet<DocumentJob> DocumentJobs => Set<DocumentJob>();
    public DbSet<DocumentJobItem> DocumentJobItems => Set<DocumentJobItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys => Set<IdempotencyKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Multi-Tenant Global Query Filters for IMustHaveProject entities & Optimistic Concurrency Tokens for IConcurrencyAware
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.ClrType != null && typeof(IMustHaveProject).IsAssignableFrom(entityType.ClrType))
            {
                ApplyProjectQueryFilter(modelBuilder, entityType.ClrType);
            }

            if (entityType.ClrType != null &&
                typeof(IConcurrencyAware).IsAssignableFrom(entityType.ClrType) &&
                entityType.FindProperty(nameof(IConcurrencyAware.Version)) is not null)
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IConcurrencyAware.Version))
                    .IsConcurrencyToken()
                    .ValueGeneratedNever();
            }
        }

        // Matching Multi-Tenant Global Query Filters for aggregate child entities (eliminates EF Core 10622)
        modelBuilder.Entity<TemplateVersion>()
            .HasQueryFilter(v => v.Template != null && IsTenantResolved && v.Template.ProjectId == CurrentTenantId);

        modelBuilder.Entity<FieldMapping>()
            .HasQueryFilter(m => m.Template != null && IsTenantResolved && m.Template.ProjectId == CurrentTenantId);

        modelBuilder.Entity<TemplateDataset>()
            .HasQueryFilter(td => td.Template != null && IsTenantResolved && td.Template.ProjectId == CurrentTenantId);

        modelBuilder.Entity<DocumentVersion>()
            .HasQueryFilter(dv => dv.Document != null && IsTenantResolved && dv.Document.ProjectId == CurrentTenantId);

        modelBuilder.Entity<DocumentJobItem>()
            .HasQueryFilter(di => di.DocumentJob != null && IsTenantResolved && di.DocumentJob.ProjectId == CurrentTenantId);
    }

    private void ApplyProjectQueryFilter(ModelBuilder modelBuilder, Type clrType)
    {
        var entityParam = Expression.Parameter(clrType, "e");
        var contextExpr = Expression.Constant(this);
        var isResolvedExpr = Expression.Property(contextExpr, nameof(IsTenantResolved));
        var currentTenantExpr = Expression.Property(contextExpr, nameof(CurrentTenantId));
        var projectIdExpr = Expression.Property(entityParam, nameof(IMustHaveProject.ProjectId));
        var tenantMatch = Expression.AndAlso(isResolvedExpr, Expression.Equal(projectIdExpr, currentTenantExpr));

        Expression filterBody;
        if (clrType == typeof(ProjectQuota) || clrType == typeof(ApiKey))
        {
            var isControlPlaneExpr = Expression.Property(contextExpr, nameof(IsControlPlane));
            var controlPlaneMatch = Expression.AndAlso(Expression.Not(isResolvedExpr), isControlPlaneExpr);
            filterBody = Expression.OrElse(tenantMatch, controlPlaneMatch);
        }
        else
        {
            filterBody = tenantMatch;
        }

        var delegateType = typeof(Func<,>).MakeGenericType(clrType, typeof(bool));
        var filterLambda = Expression.Lambda(delegateType, filterBody, entityParam);
        modelBuilder.Entity(clrType).HasQueryFilter(filterLambda);
    }
}
