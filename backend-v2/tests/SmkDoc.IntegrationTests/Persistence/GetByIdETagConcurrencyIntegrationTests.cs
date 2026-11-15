using Npgsql;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetApiKeyById;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetProjectUserById;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetUserById;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.GetBreakGlassGrantById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Collection(PostgresIntegrationSuiteDefinition.Name)]
public sealed class GetByIdETagConcurrencyIntegrationTests(PostgreSqlContainerFixture fixture)
{
    [RequiresPostgreSqlFact]
    public async Task ApiKey_GetByIdETagFlow_EnforcesOptimisticConcurrency()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "KeyEtag");
        var now = TimeProvider.System.GetUtcNow();
        var key = ApiKey.IssueStructured(
            pair.ProjectA.Id,
            ApiKeyName.Create("ETag Key"),
            "portal",
            "smk_live",
            "abcdef123456",
            "wxyz",
            new byte[32],
            ExpirationPolicy.Never,
            now);

        await using var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        var ctx = new CurrentProjectContext();
        ctx.SetProject(pair.ProjectA.Id, "key-etag-test");

        await using (var seedDb = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx))
        {
            seedDb.ApiKeys.Add(key);
            await seedDb.SaveChangesAsync();
        }

        await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx);
        var repo = new ApiKeyRepository(db);
        var uow = new UnitOfWork(db);

        var loaded = await new GetApiKeyByIdUseCase(repo).ExecuteAsync(new GetApiKeyByIdQuery(key.Id.Value), CancellationToken.None);
        loaded.Version.Should().Be(1);

        var revokeUseCase = new RevokeApiKeyUseCase(repo, uow, ctx, TimeProvider.System);
        await revokeUseCase.ExecuteAsync(new RevokeApiKeyCommand(key.Id.Value, loaded.Version), CancellationToken.None);

        var reloaded = await new GetApiKeyByIdUseCase(repo).ExecuteAsync(new GetApiKeyByIdQuery(key.Id.Value), CancellationToken.None);
        reloaded.Version.Should().BeGreaterThan(loaded.Version);
        reloaded.IsActive.Should().BeFalse();

        var staleAttempt = () => revokeUseCase.ExecuteAsync(new RevokeApiKeyCommand(key.Id.Value, loaded.Version), CancellationToken.None);
        await staleAttempt.Should().ThrowAsync<PreconditionFailedException>();
    }

    [RequiresPostgreSqlFact]
    public async Task ProjectUser_GetByIdETagFlow_EnforcesOptimisticConcurrency()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "ProjUserEtag");
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];
        var user = User.Register(EmailAddress.Create($"pu-{suffix}@example.com"), "hashed-password-placeholder", "Pu", "Tester", now);
        var role = UserProjectRole.Create(user.Id, pair.ProjectA.Id, RoleType.Developer, now);

        await using var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        var ctx = new CurrentProjectContext();
        ctx.SetProject(pair.ProjectA.Id, "proj-user-etag");

        await using (var seedDb = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx))
        {
            seedDb.Users.Add(user);
            seedDb.UserProjectRoles.Add(role);
            await seedDb.SaveChangesAsync();
        }

        await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx);
        var roleRepo = new UserProjectRoleRepository(db);
        var userRepo = new UserRepository(db);
        var uow = new UnitOfWork(db);

        var loaded = await new GetProjectUserByIdUseCase(roleRepo, userRepo)
            .ExecuteAsync(new GetProjectUserByIdQuery(user.Id.Value), CancellationToken.None);
        loaded.Version.Should().Be(1);

        var updateRoleUseCase = new UpdateUserRoleUseCase(roleRepo, userRepo, uow, TimeProvider.System);
        var updated = await updateRoleUseCase.ExecuteAsync(new UpdateUserRoleCommand(user.Id.Value, "Admin", loaded.Version), CancellationToken.None);
        updated.Version.Should().BeGreaterThan(loaded.Version);

        var reloaded = await new GetProjectUserByIdUseCase(roleRepo, userRepo)
            .ExecuteAsync(new GetProjectUserByIdQuery(user.Id.Value), CancellationToken.None);
        reloaded.Version.Should().Be(updated.Version);

        var staleAttempt = () => updateRoleUseCase.ExecuteAsync(new UpdateUserRoleCommand(user.Id.Value, "Viewer", loaded.Version), CancellationToken.None);
        await staleAttempt.Should().ThrowAsync<PreconditionFailedException>();
    }

    [RequiresPostgreSqlFact]
    public async Task GlobalUser_GetByIdETagFlow_EnforcesOptimisticConcurrency()
    {
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];
        var actor = User.Register(EmailAddress.Create($"actor-{suffix}@example.com"), "hashed-password-placeholder", "Actor", "Admin", now, SystemRole.SuperAdmin);
        var target = User.Register(EmailAddress.Create($"target-{suffix}@example.com"), "hashed-password-placeholder", "Target", "Member", now, SystemRole.Member);

        await using var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        var ctx = new CurrentProjectContext();
        ctx.SetControlPlane($"user-etag-{suffix}");
        ctx.SetActor(actor.Id, actor.SystemRole);

        await using (var seedDb = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx))
        {
            seedDb.Users.AddRange(actor, target);
            await seedDb.SaveChangesAsync();
        }

        await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx);
        var userRepo = new UserRepository(db);
        var roleRepo = new UserProjectRoleRepository(db);
        var uow = new UnitOfWork(db);

        var loaded = await new GetUserByIdUseCase(userRepo).ExecuteAsync(new GetUserByIdQuery(target.Id.Value), CancellationToken.None);
        loaded.Version.Should().Be(1);

        var setStatusUseCase = new SetUserStatusUseCase(userRepo, roleRepo, uow, ctx, TimeProvider.System);
        var updated = await setStatusUseCase.ExecuteAsync(new SetUserStatusCommand(target.Id.Value, false, loaded.Version), CancellationToken.None);
        updated.Version.Should().BeGreaterThan(loaded.Version);

        var reloaded = await new GetUserByIdUseCase(userRepo).ExecuteAsync(new GetUserByIdQuery(target.Id.Value), CancellationToken.None);
        reloaded.Version.Should().Be(updated.Version);
        reloaded.IsActive.Should().BeFalse();

        var staleAttempt = () => setStatusUseCase.ExecuteAsync(new SetUserStatusCommand(target.Id.Value, true, loaded.Version), CancellationToken.None);
        await staleAttempt.Should().ThrowAsync<PreconditionFailedException>();
    }

    [RequiresPostgreSqlFact]
    public async Task BreakGlassGrant_GetByIdETagFlow_ReflectsVersionIncrementAfterRevoke()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "BgEtag");
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];
        var superAdmin = User.Register(EmailAddress.Create($"bg-admin-{suffix}@example.com"), "hashed-password-placeholder", "Super", "Admin", now, SystemRole.SuperAdmin);
        var grant = BreakGlassGrant.Issue(pair.ProjectA.Id, superAdmin.Id, superAdmin.Id, "ETag verification", now, now.AddHours(1));

        await using var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        var ctx = new CurrentProjectContext();
        ctx.SetControlPlane($"bg-etag-{suffix}");

        await using (var seedDb = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx))
        {
            seedDb.Users.Add(superAdmin);
            seedDb.BreakGlassGrants.Add(grant);
            await seedDb.SaveChangesAsync();
        }

        await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx);
        var grantRepo = new BreakGlassGrantRepository(db);
        var uow = new UnitOfWork(db);

        var loaded = await new GetBreakGlassGrantByIdUseCase(grantRepo, TimeProvider.System)
            .ExecuteAsync(new GetBreakGlassGrantByIdQuery(grant.Id.Value), CancellationToken.None);
        loaded.Version.Should().Be(1);

        var entity = await grantRepo.GetByIdAsync(grant.Id, CancellationToken.None);
        entity!.Revoke(now.AddMinutes(5));
        grantRepo.Update(entity);
        await uow.CommitAsync(CancellationToken.None);

        var reloaded = await new GetBreakGlassGrantByIdUseCase(grantRepo, TimeProvider.System)
            .ExecuteAsync(new GetBreakGlassGrantByIdQuery(grant.Id.Value), CancellationToken.None);
        reloaded.Version.Should().BeGreaterThan(loaded.Version);
        reloaded.IsActive.Should().BeFalse();
    }
}
