using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;

public class SetUserStatusUseCaseTests
{
    private readonly UserManagementTestFixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_WhenValidInput_DeactivatesUserSuccessfully()
    {
        var projectId = Guid.NewGuid();
        var user = new UserBuilder().Build();
        var userId = user.Id;
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsDeveloper()
            .Build();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _fixture.UserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var useCase = _fixture.BuildSetUserStatusUseCase();
        var command = new SetUserStatusCommand(projectId, userId, false);
        await useCase.ExecuteAsync(command);

        user.IsActive.Should().BeFalse();
        _fixture.UserRepo.Verify(r => r.Update(user), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotInProject_ThrowsNotFoundException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProjectRole?)null);

        var useCase = _fixture.BuildSetUserStatusUseCase();
        var command = new SetUserStatusCommand(projectId, userId, true);
        var act = () => useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
