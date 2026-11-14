using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();

    private ListProjectUsersUseCase CreateSut() => new(
        _roleRepo,
        _userRepo);

    [Fact]
    public async Task ExecuteAsync_WhenUsersExist_ShouldReturnProjectUserSummaries()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        var user = new UserBuilder()
            .WithEmail("admin@smkdoc.com")
            .WithName("Somchai", "Jaidee")
            .Build();

        var role = new UserProjectRoleBuilder()
            .ForUser(user.Id)
            .InProject(projectId)
            .AsAdmin()
            .Build();

        _roleRepo.ListForCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(new List<UserProjectRole> { role });

        _userRepo.GetByIdsAsync(Arg.Any<IEnumerable<UserId>>(), Arg.Any<CancellationToken>())
            .Returns(new List<User> { user });

        var query = new ListProjectUsersQuery();

        // Act
        var result = await CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].Email.Should().Be("admin@smkdoc.com");
        result[0].FirstName.Should().Be("Somchai");
        result[0].LastName.Should().Be("Jaidee");
        result[0].Role.Should().Be("Admin");

        await _roleRepo.Received(1).ListForCurrentProjectAsync(Arg.Any<CancellationToken>());
        await _userRepo.Received(1).GetByIdsAsync(Arg.Any<IEnumerable<UserId>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextNotResolved_ShouldPropagateTenantContextNotResolvedException()
    {
        // Arrange
        _roleRepo.ListForCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<UserProjectRole>>(_ => throw new TenantContextNotResolvedException());
        var query = new ListProjectUsersQuery();

        // Act
        var act = () => CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
        await _userRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<UserId>>(), Arg.Any<CancellationToken>());
    }
}
