using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Persistence.Queries;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Collection(PostgresIntegrationSuiteDefinition.Name)]
public sealed class ZeroStandingAccessIntegrationTests(PostgreSqlContainerFixture fixture)
{
    [RequiresPostgreSqlFact]
    public async Task BreakGlassGrant_Lifecycle_EnforcesZeroStandingAccessUnderSmkApp()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "Zsa");
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];

        var superAdmin = User.Register(
            EmailAddress.Create($"superadmin-{suffix}@example.com"),
            "hashed-password-placeholder",
            "Super",
            "Admin",
            now,
            SystemRole.SuperAdmin);
        var granter = User.Register(
            EmailAddress.Create($"granter-{suffix}@example.com"),
            "hashed-password-placeholder",
            "Security",
            "Officer",
            now,
            SystemRole.SuperAdmin);

        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            var cpCtx = new CurrentProjectContext();
            cpCtx.SetControlPlane("zsa-seed");
            await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, cpCtx);
            db.Users.AddRange(superAdmin, granter);
            await db.SaveChangesAsync();
        }

        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new TenantResolutionResolver(
            memoryCache,
            NullLogger<TenantResolutionResolver>.Instance,
            appDs);

        // 1. Zero Standing Access: SuperAdmin has NO Data Plane access without an active BreakGlassGrant.
        (await resolver.HasActiveBreakGlassGrantAsync(superAdmin.Id, pair.ProjectA.Id, now, CancellationToken.None))
            .Should().BeFalse("SuperAdmin must have Zero Standing Access by default");

        // 2. Grant active 2-hour BreakGlassGrant for Project A -> allowed for A, denied for B.
        var grant = BreakGlassGrant.Issue(
            pair.ProjectA.Id,
            superAdmin.Id,
            granter.Id,
            "Investigate failed invoice batch in production",
            now,
            now.AddHours(2));

        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            var cpCtx = new CurrentProjectContext();
            cpCtx.SetControlPlane("zsa-grant");
            await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, cpCtx);
            var repo = new BreakGlassGrantRepository(db);
            await repo.AddAsync(grant, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await resolver.InvalidateBreakGlassAsync(superAdmin.Id, pair.ProjectA.Id, CancellationToken.None);

        (await resolver.HasActiveBreakGlassGrantAsync(superAdmin.Id, pair.ProjectA.Id, now.AddMinutes(1), CancellationToken.None))
            .Should().BeTrue("Active time-boxed break-glass grant unlocks target project");
        (await resolver.HasActiveBreakGlassGrantAsync(superAdmin.Id, pair.ProjectB.Id, now.AddMinutes(1), CancellationToken.None))
            .Should().BeFalse("Break-glass grant must stay strictly scoped to its TargetProjectId");

        // 3. After expiry (> 2 hours) grant is automatically invalid without manual revocation.
        await resolver.InvalidateBreakGlassAsync(superAdmin.Id, pair.ProjectA.Id, CancellationToken.None);
        (await resolver.HasActiveBreakGlassGrantAsync(superAdmin.Id, pair.ProjectA.Id, now.AddHours(3), CancellationToken.None))
            .Should().BeFalse("Expired break-glass grant must immediately deny access");

        // 4. Revoke the grant early -> immediately denied even inside original time window.
        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            var cpCtx = new CurrentProjectContext();
            cpCtx.SetControlPlane("zsa-revoke");
            await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, cpCtx);
            var repo = new BreakGlassGrantRepository(db);
            var loaded = await repo.GetByIdAsync(grant.Id, CancellationToken.None);
            loaded.Should().NotBeNull();
            loaded!.Revoke(now.AddMinutes(10));
            await db.SaveChangesAsync();
        }

        await resolver.InvalidateBreakGlassAsync(superAdmin.Id, pair.ProjectA.Id, CancellationToken.None);
        (await resolver.HasActiveBreakGlassGrantAsync(superAdmin.Id, pair.ProjectA.Id, now.AddMinutes(15), CancellationToken.None))
            .Should().BeFalse("Revoked break-glass grant must immediately revoke access");
    }
}
