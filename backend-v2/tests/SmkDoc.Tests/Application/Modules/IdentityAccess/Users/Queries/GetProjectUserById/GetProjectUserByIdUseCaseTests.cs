using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetProjectUserById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Queries.GetProjectUserById;

public sealed class GetProjectUserByIdUseCaseTests
{
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();

    private GetProjectUserByIdUseCase CreateSut() => new(_roleRepo, _userRepo);

    [Fact]
    public async Task ExecuteAsync_WhenMemberExistsInCurrentProject_ReturnsUserWithRoleVersion()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithEmail("member@smkdoc.com")
            .WithName("Suda", "Sukjai")
            .Build();
        var role = new UserProjectRoleBuilder()
            .ForUser(user.Id)
            .InProject(projectId)
            .AsDeveloper()
            .Build();
        role.UpdateRole(RoleType.Admin, TestConstants.BaselineTime.AddMinutes(5));

        _roleRepo.GetForCurrentProjectAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(role);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetProjectUserByIdQuery(user.Id.Value), CancellationToken.None);

        // Assert
        result.Id.Should().Be(user.Id.Value);
        result.Email.Should().Be("member@smkdoc.com");
        result.Role.Should().Be("Admin");
        result.Version.Should().Be(role.Version);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipMissing_ThrowsNotFoundException()
    {
        // Arrange
        var userId = UserId.New();
        _roleRepo.GetForCurrentProjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns((UserProjectRole?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetProjectUserByIdQuery(userId.Value), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
