using Microsoft.EntityFrameworkCore;
using SmkDocServer.Domain.Entities;

namespace SmkDocServer.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TemplateMetadata> Templates { get; set; }
    public DbSet<TemplateVersion> TemplateVersions { get; set; }
    public DbSet<FieldMapping> FieldMappings { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<GenerationLog> GenerationLogs { get; set; }
    public DbSet<DataConnection> DataConnections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Project
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
        });

        // ApiKey
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.ApiKeys)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // GenerationLog
        modelBuilder.Entity<GenerationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.TemplateName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.OutputFormat).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.GenerationLogs)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ApiKey)
                  .WithMany()
                  .HasForeignKey(e => e.ApiKeyId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // TemplateMetadata
        modelBuilder.Entity<TemplateMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProjectId, e.FileName }).IsUnique();
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(255);

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Templates)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TemplateVersion
        modelBuilder.Entity<TemplateVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateId, e.VersionNumber }).IsUnique();
            entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(255);

            entity.HasOne(e => e.Template)
                  .WithMany(t => t.Versions)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // FieldMapping
        modelBuilder.Entity<FieldMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TemplateId, e.Placeholder }).IsUnique();
            entity.Property(e => e.Placeholder).IsRequired().HasMaxLength(150);
            entity.Property(e => e.SourcePath).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.PlaceholderType).HasMaxLength(20).HasDefaultValue("text");
            entity.Property(e => e.Transform).HasMaxLength(50);
            entity.Property(e => e.DefaultValue).HasMaxLength(500);
            entity.Property(e => e.DataSourceType).HasMaxLength(10).HasDefaultValue("json");
            entity.Property(e => e.SqlQuery).HasColumnType("text");

            entity.HasOne(e => e.Template)
                  .WithMany(t => t.FieldMappings)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DataConnection)
                  .WithMany()
                  .HasForeignKey(e => e.DataConnectionId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // DataConnection
        modelBuilder.Entity<DataConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.DatabaseType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ConnectionStringEncrypted).IsRequired().HasColumnType("text");

            entity.HasOne(e => e.Project)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
