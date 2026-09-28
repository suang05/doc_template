using FluentAssertions;
using Moq;
using SmkDoc.Application.UseCases.Users.Commands.SetUserStatus;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Users;

public class SetUserStatusUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly SetUserStatusUseCase _useCase;

    public SetUserStatusUseCaseTests()
    {
        _useCase = new SetUserStatusUseCase(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _uowMock.Object,
            new SetUserStatusCommandValidator());
    }

    [Fact]
    public async Task ExecuteAsync_DeactivatesUserSuccessfully()
    {
        var projectId = Guid.NewGuid();
        var user = new User("user@test.com", "hash", "First", "Last");
        var userId = user.Id;
        var roleEntry = new UserProjectRole(userId, projectId, RoleType.Developer);

        _roleRepoMock.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new SetUserStatusCommand(projectId, userId, false);
        await _useCase.ExecuteAsync(command);

        user.IsActive.Should().BeFalse();
        _userRepoMock.Verify(r => r.Update(user), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotInProject_ThrowsNotFoundException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _roleRepoMock.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProjectRole?)null);

        var command = new SetUserStatusCommand(projectId, userId, true);
        Func<Task> act = () => _useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
