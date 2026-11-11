using NSubstitute;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private ListProjectUsersUseCase CreateSut() => new(
        _roleRepo,
        _userRepo,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenUsersExist_ShouldReturnProjectUserSummaries()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var user = new UserBuilder()
            .WithEmail("admin@smkdoc.com")
            .WithName("Somchai", "Jaidee")
            .Build();

        var role = new UserProjectRoleBuilder()
            .ForUser(user.Id)
            .InProject(projectId)
            .AsAdmin()
            .Build();

        _roleRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<UserProjectRole> { role });

        _userRepo.GetByIdsAsync(Arg.Any<IEnumerable<UserId>>(), Arg.Any<CancellationToken>())
            .Returns(new List<User> { user });

        var query = new ListProjectUsersQuery();

        // Act
        var result = await CreateSut().ExecuteAsync(query);

        // Assert
        result.Should().ContainSingle();
        result[0].Email.Should().Be("admin@smkdoc.com");
        result[0].FirstName.Should().Be("Somchai");
        result[0].LastName.Should().Be("Jaidee");
        result[0].Role.Should().Be("Admin");

        await _roleRepo.Received(1).ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>());
        await _userRepo.Received(1).GetByIdsAsync(Arg.Any<IEnumerable<UserId>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextNotResolved_ShouldThrowTenantContextRequiredException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.ProjectId.Returns(_ => throw new TenantContextRequiredException());
        var query = new ListProjectUsersQuery();

        // Act
        var act = () => CreateSut().ExecuteAsync(query);

        // Assert
        await act.Should().ThrowAsync<TenantContextRequiredException>();
        await _roleRepo.DidNotReceive().ListByProjectAsync(Arg.Any<ProjectId>(), Arg.Any<CancellationToken>());
        await _userRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<UserId>>(), Arg.Any<CancellationToken>());
    }
}
