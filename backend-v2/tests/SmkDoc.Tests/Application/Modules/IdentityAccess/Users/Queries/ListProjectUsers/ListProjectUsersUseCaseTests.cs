using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

public class ListProjectUsersUseCaseTests
{
    private readonly UserManagementTestFixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_WhenUsersExist_ReturnsUsersInProject()
    {
        var projectId = Guid.NewGuid();
        var user = new UserBuilder().WithEmail("admin@test.com").WithName("Admin", "User").Build();
        var role = new UserProjectRoleBuilder().ForUser(user.Id).InProject(projectId).AsAdmin().Build();

        _fixture.RoleRepo.Setup(r => r.ListByProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProjectRole> { role });
        _fixture.UserRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });

        var useCase = _fixture.BuildListProjectUsersUseCase();
        var result = await useCase.ExecuteAsync(new ListProjectUsersQuery(projectId));

        result.Should().ContainSingle();
        result[0].Email.Should().Be("admin@test.com");
        result[0].Role.Should().Be("Admin");
    }
}
