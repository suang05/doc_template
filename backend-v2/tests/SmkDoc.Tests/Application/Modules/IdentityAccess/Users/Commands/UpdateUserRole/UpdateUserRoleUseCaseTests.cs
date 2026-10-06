using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;

public class UpdateUserRoleUseCaseTests
{
    private readonly UserManagementTestFixture _fixture = new();

    private UpdateUserRoleUseCase CreateSut() => _fixture.BuildUpdateUserRoleUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenValidInput_UpdatesRoleSuccessfully()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);

        var command = new UpdateUserRoleCommand(projectId, userId, "Developer");
        await CreateSut().ExecuteAsync(command);

        roleEntry.Role.Should().Be(RoleType.Developer);
        _fixture.RoleRepo.Verify(r => r.Update(roleEntry), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLastAdminDemoted_ThrowsConflictException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsAdmin()
            .Build();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _fixture.RoleRepo.Setup(r => r.CountAdminsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateUserRoleCommand(projectId, userId, "Viewer");
        var act = () => CreateSut().ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotInProject_ThrowsNotFoundException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProjectRole?)null);

        var command = new UpdateUserRoleCommand(projectId, userId, "Developer");
        var act = () => CreateSut().ExecuteAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
