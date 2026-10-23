using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Application.Common.Interfaces;

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

