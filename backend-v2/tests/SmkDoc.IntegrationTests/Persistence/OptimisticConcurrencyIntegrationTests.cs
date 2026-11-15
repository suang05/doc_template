using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Collection(PostgresIntegrationSuiteDefinition.Name)]
public sealed class OptimisticConcurrencyIntegrationTests(PostgreSqlContainerFixture fixture)
{
    [RequiresPostgreSqlFact]
    public async Task UnitOfWork_WhenConcurrentUpdateDetectedOnVersionToken_ThrowsPreconditionFailedException()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "Occ");
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];
        var seeded = await PostgresIntegrationTestSupport.SeedTemplateAsync(
            fixture,
            pair.ProjectA.Id,
            $"Original Title {suffix}",
            $"occ-{suffix}");

        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();

        var ctx1 = new CurrentProjectContext();
        ctx1.SetProject(pair.ProjectA.Id, "writer-1");
        var ctx2 = new CurrentProjectContext();
        ctx2.SetProject(pair.ProjectA.Id, "writer-2");

        await using var db1 = PostgresIntegrationTestSupport.CreateDbContext(appDs, ctx1);
        await using var db2 = PostgresIntegrationTestSupport.CreateDbContext(appDs, ctx2);

        var t1 = await db1.Templates.FirstAsync(t => t.Id == seeded.Id);
        var t2 = await db2.Templates.FirstAsync(t => t.Id == seeded.Id);

        t1.Version.Should().Be(1);
        t2.Version.Should().Be(1);

        var now = TimeProvider.System.GetUtcNow();
        t1.UpdateDetails(TemplateName.Create($"Winner Title {suffix}"), "cat-1", now);
        var uow1 = new UnitOfWork(db1);
        await uow1.CommitAsync(CancellationToken.None);

        t1.Version.Should().Be(2);

        // Writer 2 still holds Version = 1 in its tracked change set -> Postgres WHERE "Version" = 1 matches 0 rows.
        t2.UpdateDetails(TemplateName.Create($"Stale Title {suffix}"), "cat-2", now.AddSeconds(1));
        var uow2 = new UnitOfWork(db2);

        var act = () => uow2.CommitAsync(CancellationToken.None);
        var ex = await act.Should().ThrowAsync<PreconditionFailedException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.PreconditionFailed);

        // Verify winner's state remained intact in PostgreSQL.
        var verifyCtx = new CurrentProjectContext();
        verifyCtx.SetProject(pair.ProjectA.Id, "verifier");
        await using var verifyDb = PostgresIntegrationTestSupport.CreateDbContext(appDs, verifyCtx);
        var persisted = await verifyDb.Templates.AsNoTracking().FirstAsync(t => t.Id == seeded.Id);
        persisted.Name.Value.Should().Be($"Winner Title {suffix}");
        persisted.Version.Should().Be(2);
    }
}
