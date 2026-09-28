using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Users;

public class RemoveUserUseCaseTests
{
    private readonly UserManagementTestFixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_RemovesUserSuccessfully()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsViewer()
            .Build();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);

        var useCase = _fixture.BuildRemoveUserUseCase();
        var command = new RemoveUserCommand(projectId, userId, currentUserId);
        
        await useCase.ExecuteAsync(command);

        _fixture.RoleRepo.Verify(r => r.Remove(roleEntry), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LastAdmin_ThrowsConflictException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var roleEntry = new UserProjectRoleBuilder()
            .InProject(projectId)
            .ForUser(userId)
            .AsAdmin()
            .Build();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _fixture.RoleRepo.Setup(r => r.CountAdminsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var useCase = _fixture.BuildRemoveUserUseCase();
        var command = new RemoveUserCommand(projectId, userId, currentUserId);
        
        Func<Task> act = () => useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExecuteAsync_UserNotInProject_ThrowsNotFoundException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _fixture.RoleRepo.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProjectRole?)null);

        var useCase = _fixture.BuildRemoveUserUseCase();
        var command = new RemoveUserCommand(projectId, userId, currentUserId);

        Func<Task> act = () => useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
