using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SmkDoc.Domain.Entities;
using SmkDoc.Infrastructure.Migrations;
using SmkDoc.Infrastructure.Persistence;

namespace SmkDoc.Tests.Infrastructure.Persistence;

public sealed class EfCoreModelParityTests
{
    private static AppDbContext CreateDesignTimeDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=smkdoc_parity_check;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void AppDbContextModelSnapshot_Must_Have_Zero_Pending_Differences_Against_AppDbContext()
    {
        using var context = CreateDesignTimeDbContext();

        var designTimeModel = context.GetService<IDesignTimeModel>();
        var modelDiffer = context.GetService<IMigrationsModelDiffer>();
        var modelRuntimeInitializer = context.GetService<IModelRuntimeInitializer>();
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();

        migrationsAssembly.ModelSnapshot.Should().NotBeNull();
        migrationsAssembly.ModelSnapshot.Should().BeOfType<AppDbContextModelSnapshot>();

        var finalizedSnapshotModel = modelRuntimeInitializer.Initialize(
            migrationsAssembly.ModelSnapshot!.Model,
            designTime: true,
            validationLogger: null);

        var differences = modelDiffer.GetDifferences(
            finalizedSnapshotModel.GetRelationalModel(),
            designTimeModel.Model.GetRelationalModel());

        var formattedDiffs = differences
            .Select(FormatOperation)
            .ToList();

        formattedDiffs.Should().BeEmpty(
            $"AppDbContextModelSnapshot must match AppDbContext 100% with zero pending migration operations. Found: {string.Join(" | ", formattedDiffs)}");
    }

    [Fact]
    public void TenantAndAuditHotPathIndexes_And_JsonbMappingsSnapshot_Must_Be_Configured()
    {
        using var context = CreateDesignTimeDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        // 1. templates: composite unique (ProjectId, Slug), no global single-column unique Slug index
        var templateEntity = model.FindEntityType(typeof(Template))!;
        var templateIndexes = templateEntity.GetIndexes().ToList();

        templateIndexes.Should().Contain(i =>
            i.IsUnique &&
            i.Properties.Count == 2 &&
            i.Properties[0].Name == nameof(Template.ProjectId) &&
            i.Properties[1].Name == nameof(Template.Slug));

        templateIndexes.Should().NotContain(i =>
            i.IsUnique &&
            i.Properties.Count == 1 &&
            i.Properties[0].Name == nameof(Template.Slug));

        // 2. template_versions: MappingsSnapshot mapped to jsonb
        var versionEntity = model.FindEntityType(typeof(TemplateVersion))!;
        var mappingsSnapshotProp = versionEntity.FindProperty(nameof(TemplateVersion.MappingsSnapshot))!;
        mappingsSnapshotProp.GetColumnType().Should().Be("jsonb");

        // 3. generation_logs: composite descending indexes for tenant log queries & callerApp filters
        var logEntity = model.FindEntityType(typeof(GenerationLog))!;
        var logIndexes = logEntity.GetIndexes().ToList();

        var tenantTimeIndex = logIndexes.SingleOrDefault(i =>
            i.Properties.Count == 2 &&
            i.Properties[0].Name == nameof(GenerationLog.ProjectId) &&
            i.Properties[1].Name == nameof(GenerationLog.CreatedAt));
        tenantTimeIndex.Should().NotBeNull();
        tenantTimeIndex!.IsDescending.Should().Equal(false, true);

        var tenantCallerTimeIndex = logIndexes.SingleOrDefault(i =>
            i.Properties.Count == 3 &&
            i.Properties[0].Name == nameof(GenerationLog.ProjectId) &&
            i.Properties[1].Name == nameof(GenerationLog.CallerApp) &&
            i.Properties[2].Name == nameof(GenerationLog.CreatedAt));
        tenantCallerTimeIndex.Should().NotBeNull();
        tenantCallerTimeIndex!.IsDescending.Should().Equal(false, false, true);
    }

    private static string FormatOperation(MigrationOperation op) => op switch
    {
        AddColumnOperation c => $"AddColumn({c.Table}.{c.Name}:{c.ColumnType})",
        DropColumnOperation c => $"DropColumn({c.Table}.{c.Name})",
        AlterColumnOperation c => $"AlterColumn({c.Table}.{c.Name}: oldType={c.OldColumn.ColumnType}, newType={c.ColumnType}, oldNull={c.OldColumn.IsNullable}, newNull={c.IsNullable}, oldDefault={c.OldColumn.DefaultValue ?? c.OldColumn.DefaultValueSql}, newDefault={c.DefaultValue ?? c.DefaultValueSql}, oldMaxLen={c.OldColumn.MaxLength}, newMaxLen={c.MaxLength})",
        CreateIndexOperation i => $"CreateIndex({i.Table}.{i.Name}: cols=[{string.Join(",", i.Columns)}], unique={i.IsUnique})",
        DropIndexOperation i => $"DropIndex({i.Table}.{i.Name})",
        AddForeignKeyOperation fk => $"AddFK({fk.Table}.{fk.Name} -> {fk.PrincipalTable}, delete={fk.OnDelete})",
        DropForeignKeyOperation fk => $"DropFK({fk.Table}.{fk.Name})",
        _ => $"{op.GetType().Name}"
    };
}
