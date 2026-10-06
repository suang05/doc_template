using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUserService = null) : base(options) 
    { 
        _currentUserService = currentUserService;
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProjectRole> UserProjectRoles => Set<UserProjectRole>();

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

        // 0. Auth & Multi-Tenancy
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200)
                  .HasConversion(n => n.Value, v => CompanyName.Create(v));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200)
                  .HasConversion(n => n.Value, v => ProjectName.Create(v));
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100)
                  .HasConversion(s => s.Value, v => new TemplateSlug(v));
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne<Company>()
                  .WithMany(c => c.Projects)
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255)
                  .HasConversion(m => m.Value, v => EmailAddress.Create(v));
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.SystemRole)
                  .HasConversion(r => r.Name, v => SystemRole.FromDisplayName<SystemRole>(v))
                  .HasMaxLength(30)
                  .HasDefaultValue(SystemRole.Member);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });


        modelBuilder.Entity<UserProjectRole>(entity =>
        {
            entity.ToTable("user_project_roles");
            entity.HasKey(e => new { e.UserId, e.ProjectId });
            entity.Property(e => e.Role)
                  .HasConversion(r => r.Name, v => RoleType.FromDisplayName<RoleType>(v))
                  .HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne<User>()
                  .WithMany(u => u.ProjectRoles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Project>()
                  .WithMany(p => p.UserRoles)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 1. Templates
        modelBuilder.Entity<Template>(entity =>
        {
            entity.ToTable("templates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100)
                  .HasConversion(v => v.Value, v => TemplateName.Create(v));
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Slug)
                  .HasConversion(s => s.Value, v => new TemplateSlug(v))
                  .IsRequired()
                  .HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

            // Global Query Filter for Data Isolation (Multi-Tenancy)
            if (_currentUserService != null)
            {
                entity.HasQueryFilter(e => e.ProjectId == _currentUserService.ProjectId);
            }

            entity.HasOne(e => e.CurrentVersion)
                  .WithMany()
                  .HasForeignKey(e => e.CurrentVersionId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Project>()
                  .WithMany(p => p.Templates)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 2. Field Mappings
        modelBuilder.Entity<FieldMapping>(entity =>
        {
            entity.ToTable("field_mappings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateId, e.Placeholder }).IsUnique();
            entity.Property(e => e.Placeholder).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SourcePath).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Transform).HasMaxLength(50);
            entity.Property(e => e.DataSourceType)
                  .HasConversion(d => d.Value, v => DataSourceType.FromString(v))
                  .HasMaxLength(20).HasDefaultValue(DataSourceType.Json);
            entity.Property(e => e.DatasetAlias).HasMaxLength(50)
                  .HasConversion(a => a == null ? null : a.Value, v => v == null ? null : DatasetAlias.Create(v));
            entity.Property(e => e.ResultPath).HasMaxLength(300);
            entity.Property(e => e.MathExpression).HasMaxLength(500);

            entity.HasOne(e => e.Template)
                  .WithMany(t => t.FieldMappings)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 3. Template Versions
        modelBuilder.Entity<TemplateVersion>(entity =>
        {
            entity.ToTable("template_versions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateId, e.Version }).IsUnique();
            entity.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status)
                  .HasConversion(s => s.Id, v => TemplateVersionStatus.FromValue<TemplateVersionStatus>(v))
                  .HasDefaultValue(TemplateVersionStatus.Draft);
            entity.Property(e => e.FileFormat)
                  .HasConversion(f => f == null ? null : f.Name, v => v == null ? null : TemplateFormat.FromDisplayName<TemplateFormat>(v))
                  .HasMaxLength(10);
            entity.Property(e => e.DataSchema).HasColumnType("jsonb");
            entity.Property(e => e.SamplePayload).HasColumnType("jsonb");
            entity.Property(e => e.CommitMessage).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Template)
                  .WithMany(t => t.Versions)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. API Keys
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100)
                  .HasConversion(v => v.Value, v => ApiKeyName.Create(v));
            entity.Property(e => e.CallerApp).IsRequired().HasMaxLength(50);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(255)
                  .HasConversion(v => v.Value, v => new Sha256Hash(v));
            entity.Property(e => e.Expiration)
                  .HasColumnName("expires_at")
                  .HasConversion(v => v.ExpiresAt, v => ExpirationPolicy.FromExisting(v));
            entity.Ignore(e => e.IsRevoked);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });

        // 5. Generation Logs
        modelBuilder.Entity<GenerationLog>(entity =>
        {
            entity.ToTable("generation_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.CallerApp).HasMaxLength(50);
            entity.Property(e => e.TriggerSource).HasMaxLength(20);
            entity.Property(e => e.OutputKey).HasMaxLength(500);
            entity.Property(e => e.OutputFormat)
                  .HasConversion(f => f == null ? null : f.Name, v => v == null ? null : OutputFormat.FromDisplayName<OutputFormat>(v))
                  .HasMaxLength(10);
            entity.Property(e => e.PayloadHashSha256)
                  .HasConversion(h => h == null ? null : h.Value, v => v == null ? null : new Sha256Hash(v))
                  .HasMaxLength(64);
            entity.Property(e => e.Status)
                  .HasConversion(s => s.Name, v => GenerationStatus.FromName(v))
                  .IsRequired()
                  .HasMaxLength(20);
            entity.Property(e => e.InputData).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne<Template>()
                  .WithMany()
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<TemplateVersion>()
                  .WithMany()
                  .HasForeignKey(e => e.TemplateVersionId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<ApiKey>()
                  .WithMany()
                  .HasForeignKey(e => e.ApiKeyId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 6. Documents (anchor entity for versioned document references)
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DocumentRef).IsUnique();
            entity.Property(e => e.DocumentRef)
                  .HasConversion(d => d.Value, v => DocumentReference.Create(v))
                  .IsRequired()
                  .HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne<Template>()
                  .WithMany()
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 7. Document Versions (audit trail linked to Document entity)
        modelBuilder.Entity<DocumentVersion>(entity =>
        {
            entity.ToTable("document_versions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DocumentId, e.Version }).IsUnique();
            entity.Property(e => e.ChangeNote).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne<Document>()
                  .WithMany(d => d.Versions)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<TemplateVersion>()
                  .WithMany()
                  .HasForeignKey(e => e.TemplateVersionId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<GenerationLog>()
                  .WithMany()
                  .HasForeignKey(e => e.GenerationLogId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 8. Data Connections
        modelBuilder.Entity<DataConnection>(entity =>
        {
            entity.ToTable("data_connections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100)
                  .HasConversion(v => v.Value, v => ConnectionName.Create(v));
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50)
                  .HasConversion(p => p.Name, v => SmkDoc.Domain.Common.Enumeration.FromDisplayName<DatabaseProvider>(v));
            entity.Property(e => e.EncryptedConnectionString).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });

        // 9. Datasets
        modelBuilder.Entity<Dataset>(entity =>
        {
            entity.ToTable("datasets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200)
                  .HasConversion(v => v.Value, v => DatasetName.Create(v));
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SqlQuery).IsRequired().HasColumnType("text");
            entity.Property(e => e.CacheSeconds).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.DataConnection)
                  .WithMany()
                  .HasForeignKey(e => e.DataConnectionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 10. Template Datasets (SSRS-style alias assignments)
        modelBuilder.Entity<TemplateDataset>(entity =>
        {
            entity.ToTable("template_datasets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateId, e.Alias }).IsUnique();
            entity.Property(e => e.Alias).IsRequired().HasMaxLength(50)
                  .HasConversion(a => a.Value, v => DatasetAlias.Create(v));

            entity.HasOne(e => e.Template)
                  .WithMany(t => t.TemplateDatasets)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Dataset>()
                  .WithMany()
                  .HasForeignKey(e => e.DatasetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
