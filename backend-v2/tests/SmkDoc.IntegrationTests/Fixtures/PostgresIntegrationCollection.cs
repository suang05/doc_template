using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Persistence;
using SmkDoc.Infrastructure.Persistence.Interceptors;
using SmkDoc.Infrastructure.Persistence.Outbox;

namespace SmkDoc.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class PostgresIntegrationSuiteDefinition : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "PostgresIntegration";
}

internal sealed record SeededTenantPair(
    CompanyId CompanyId,
    Project ProjectA,
    Project ProjectB);

internal static class PostgresIntegrationTestSupport
{
    public static AppDbContext CreateDbContext(
        NpgsqlDataSource dataSource,
        CurrentProjectContext projectContext,
        TimeProvider? timeProvider = null)
    {
        var effectiveTime = timeProvider ?? TimeProvider.System;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UsePostgreSqlConventions(dataSource)
            .AddInterceptors(
                new TenantDbConnectionInterceptor(projectContext),
                new TenantDbTransactionInterceptor(projectContext),
                new TenantSecuritySaveChangesInterceptor(projectContext),
                new OutboxSaveChangesInterceptor(effectiveTime))
            .Options;

        return new AppDbContext(options, projectContext);
    }

    public static AppDbContext CreatePlainDbContext(string connectionString, CurrentProjectContext projectContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UsePostgreSqlConventions(connectionString)
            .AddInterceptors(
                new TenantDbConnectionInterceptor(projectContext),
                new TenantDbTransactionInterceptor(projectContext),
                new TenantSecuritySaveChangesInterceptor(projectContext),
                new OutboxSaveChangesInterceptor(TimeProvider.System))
            .Options;

        return new AppDbContext(options, projectContext);
    }

    public static async Task<SeededTenantPair> SeedTwoProjectsAsync(
        PostgreSqlContainerFixture fixture,
        string prefix,
        CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^12..];

        var context = new CurrentProjectContext();
        context.SetControlPlane("integration-seed");

        await using var dataSource = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        await using var db = CreateDbContext(dataSource, context);

        var company = Company.Create(CompanyName.Create($"{prefix}-Co-{suffix}"), now);
        db.Companies.Add(company);

        var projectA = Project.Create(
            company.Id,
            ProjectName.Create($"{prefix}-ProjA-{suffix}"),
            ProjectSlug.Create($"p-{suffix}-a"),
            now);
        var projectB = Project.Create(
            company.Id,
            ProjectName.Create($"{prefix}-ProjB-{suffix}"),
            ProjectSlug.Create($"p-{suffix}-b"),
            now);

        db.Projects.AddRange(projectA, projectB);
        db.ProjectQuotas.AddRange(
            ProjectQuota.CreateDefault(projectA.Id, now),
            ProjectQuota.CreateDefault(projectB.Id, now));

        await db.SaveChangesAsync(ct);

        return new SeededTenantPair(company.Id, projectA, projectB);
    }

    public static async Task<Template> SeedTemplateAsync(
        PostgreSqlContainerFixture fixture,
        ProjectId projectId,
        string name,
        string slug,
        CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow();
        var context = new CurrentProjectContext();
        context.SetProject(projectId, "integration-seed");

        await using var dataSource = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        await using var db = CreateDbContext(dataSource, context);

        var template = Template.Create(
            projectId,
            TemplateName.Create(name),
            TemplateSlug.Create(slug),
            null,
            now);

        db.Templates.Add(template);
        await db.SaveChangesAsync(ct);
        return template;
    }
}
