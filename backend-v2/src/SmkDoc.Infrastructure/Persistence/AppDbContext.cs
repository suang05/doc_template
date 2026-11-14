using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // 11. Multi-Tenant Global Query Filters for IMustHaveProject entities & Concurrency Tokens for IConcurrencyAware entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.ClrType != null && typeof(IMustHaveProject).IsAssignableFrom(entityType.ClrType))
            {
                ApplyProjectQueryFilter(modelBuilder, entityType.ClrType);
            }

            if (entityType.ClrType != null &&
                typeof(IConcurrencyAware).IsAssignableFrom(entityType.ClrType) &&
                entityType.FindProperty(nameof(IConcurrencyAware.UpdatedAt)) is not null)
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IConcurrencyAware.UpdatedAt))
                    .IsConcurrencyToken();
            }
        }

        // 12. Matching Multi-Tenant Global Query Filters for aggregate child entities (eliminates EF Core 10622)
        modelBuilder.Entity<TemplateVersion>()
            .HasQueryFilter(v => v.Template != null && IsTenantResolved && v.Template.ProjectId == CurrentTenantId);

        modelBuilder.Entity<FieldMapping>()
            .HasQueryFilter(m => m.Template != null && IsTenantResolved && m.Template.ProjectId == CurrentTenantId);

        modelBuilder.Entity<TemplateDataset>()
            .HasQueryFilter(td => td.Template != null && IsTenantResolved && td.Template.ProjectId == CurrentTenantId);

        modelBuilder.Entity<DocumentVersion>()
            .HasQueryFilter(dv => dv.Document != null && IsTenantResolved && dv.Document.ProjectId == CurrentTenantId);
    }

    private void ApplyProjectQueryFilter(ModelBuilder modelBuilder, Type clrType)
    {
        var entityParam = Expression.Parameter(clrType, "e");
        var contextExpr = Expression.Constant(this);
        var isResolvedExpr = Expression.Property(contextExpr, nameof(IsTenantResolved));
        var currentTenantExpr = Expression.Property(contextExpr, nameof(CurrentTenantId));
        var projectIdExpr = Expression.Property(entityParam, nameof(IMustHaveProject.ProjectId));
        var filterBody = Expression.AndAlso(isResolvedExpr, Expression.Equal(projectIdExpr, currentTenantExpr));
        var delegateType = typeof(Func<,>).MakeGenericType(clrType, typeof(bool));
        var filterLambda = Expression.Lambda(delegateType, filterBody, entityParam);
        modelBuilder.Entity(clrType).HasQueryFilter(filterLambda);
    }
}
