using FluentAssertions;
using Moq;
using SmkDoc.Application.UseCases.Users.Queries.ListProjectUsers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Users;

public class ListProjectUsersUseCaseTests
{
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly ListProjectUsersUseCase _useCase;

    public ListProjectUsersUseCaseTests()
    {
        _useCase = new ListProjectUsersUseCase(
            _roleRepoMock.Object,
            _userRepoMock.Object,
            new ListProjectUsersQueryValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUsersInProject()
    {
        var projectId = Guid.NewGuid();
        var user = new User("admin@test.com", "hash", "Admin", "User");
        var userId = user.Id;
        var roles = new List<UserProjectRole>
        {
            new(userId, projectId, RoleType.Admin)
        };
        var users = new List<User> { user };

        _roleRepoMock.Setup(r => r.ListByProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);
        _userRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _useCase.ExecuteAsync(new ListProjectUsersQuery(projectId));

        result.Should().ContainSingle();
        result[0].Email.Should().Be("admin@test.com");
        result[0].Role.Should().Be("Admin");
    }
}
