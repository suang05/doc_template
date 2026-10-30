using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
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

    public ProjectId CurrentTenantId => _projectContext?.ProjectId ?? ProjectId.Empty;
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

        // 11. Multi-Tenant Global Query Filters for IMustHaveProject entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.ClrType != null && typeof(IMustHaveProject).IsAssignableFrom(entityType.ClrType))
            {
                ConfigureProjectFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder]);
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

    private static readonly MethodInfo ConfigureProjectFilterMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureProjectFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void ConfigureProjectFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMustHaveProject
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => IsTenantResolved && e.ProjectId == CurrentTenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEventEntities = ChangeTracker.Entries<IHaveDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in domainEventEntities)
        {
            entity.ClearDomainEvents();
        }

        return result;
    }
}
