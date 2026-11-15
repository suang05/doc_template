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
            .UsePostgreSqlConventions("Host=localhost;Database=smkdoc_parity_check;Username=postgres;Password=postgres")
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

    [Fact]
    public void Phase4Entities_OutboxIdempotencyGovernanceAndJobQueue_MustBeMapped()
    {
        using var context = CreateDesignTimeDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var expectedTables = new Dictionary<Type, string>
        {
            [typeof(SmkDoc.Infrastructure.Persistence.Outbox.OutboxMessage)] = "outbox_messages",
            [typeof(SmkDoc.Infrastructure.Persistence.Idempotency.IdempotencyKeyEntity)] = "idempotency_keys",
            [typeof(SmkDoc.Domain.TenantManagement.ProjectQuota)] = "project_quotas",
            [typeof(SmkDoc.Domain.TenantManagement.Font)] = "fonts",
            [typeof(SmkDoc.Domain.TenantManagement.BreakGlassGrant)] = "break_glass_grants",
            [typeof(DocumentJob)] = "document_jobs",
            [typeof(DocumentJobItem)] = "document_job_items"
        };

        foreach (var (entityClrType, expectedTableName) in expectedTables)
        {
            var entityType = model.FindEntityType(entityClrType);
            entityType.Should().NotBeNull($"{entityClrType.Name} must be mapped in AppDbContext");
            entityType!.GetTableName().Should().Be(expectedTableName);
        }

        var jobItemEntity = model.FindEntityType(typeof(DocumentJobItem))!;
        var jobFk = jobItemEntity.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(DocumentJob));
        jobFk.Properties.Single().Name.Should().Be(nameof(DocumentJobItem.DocumentJobId));
        jobFk.DeleteBehavior.Should().Be(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
    }

    [Fact]
    public void RelationalSchema_MustUsePureLowercaseSnakeCaseIdentifiers()
    {
        using var context = CreateDesignTimeDbContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var violations = new List<string>();
        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName))
            {
                continue;
            }

            if (tableName.Any(char.IsUpper))
            {
                violations.Add($"Table '{tableName}'");
            }

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());

            foreach (var property in entityType.GetProperties())
            {
                var colName = property.GetColumnName(storeObject);
                if (!string.IsNullOrEmpty(colName) && colName.Any(char.IsUpper))
                {
                    violations.Add($"Column '{tableName}.{colName}'");
                }

                var computedSql = property.GetComputedColumnSql(storeObject);
                if (HasUppercaseSqlIdentifier(computedSql))
                {
                    violations.Add($"ComputedSql '{tableName}.{colName}': {computedSql}");
                }
            }

            foreach (var key in entityType.GetKeys())
            {
                var keyName = key.GetName(storeObject);
                if (!string.IsNullOrEmpty(keyName) && keyName.Any(char.IsUpper))
                {
                    violations.Add($"Key '{tableName}.{keyName}'");
                }
            }

            foreach (var fk in entityType.GetForeignKeys())
            {
                var principalTable = fk.PrincipalEntityType.GetTableName();
                if (string.IsNullOrEmpty(principalTable))
                {
                    continue;
                }

                var principalStoreObject = StoreObjectIdentifier.Table(principalTable, fk.PrincipalEntityType.GetSchema());
                var fkName = fk.GetConstraintName(storeObject, principalStoreObject);
                if (!string.IsNullOrEmpty(fkName) && fkName.Any(char.IsUpper))
                {
                    violations.Add($"ForeignKey '{tableName}.{fkName}'");
                }
            }

            foreach (var index in entityType.GetIndexes())
            {
                var indexName = index.GetDatabaseName(storeObject);
                if (!string.IsNullOrEmpty(indexName) && indexName.Any(char.IsUpper))
                {
                    violations.Add($"Index '{tableName}.{indexName}'");
                }

                var filterSql = index.GetFilter(storeObject);
                if (HasUppercaseSqlIdentifier(filterSql))
                {
                    violations.Add($"IndexFilter '{tableName}.{indexName}': {filterSql}");
                }
            }
        }

        violations.Should().BeEmpty(
            $"All PostgreSQL tables, columns, keys, constraints, indexes, filters, and computed SQL must use pure lowercase snake_case. Found: {string.Join(" | ", violations)}");
    }

    private static bool HasUppercaseSqlIdentifier(string? sql) =>
        !string.IsNullOrWhiteSpace(sql) &&
        (System.Text.RegularExpressions.Regex.IsMatch(sql, "\"[^\"]*[A-Z][^\"]*\"") ||
         System.Text.RegularExpressions.Regex.IsMatch(sql, @"\b[A-Z][a-z]+[A-Za-z]*\b"));

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
