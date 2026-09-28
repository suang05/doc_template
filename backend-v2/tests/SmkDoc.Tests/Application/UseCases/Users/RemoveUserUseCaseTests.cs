using FluentAssertions;
using Moq;
using SmkDoc.Application.UseCases.Users.Commands.RemoveUser;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Users;

public class RemoveUserUseCaseTests
{
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly RemoveUserUseCase _useCase;

    public RemoveUserUseCaseTests()
    {
        _useCase = new RemoveUserUseCase(
            _roleRepoMock.Object,
            _uowMock.Object,
            new RemoveUserCommandValidator());
    }

    [Fact]
    public async Task ExecuteAsync_RemovesUserSuccessfully()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var roleEntry = new UserProjectRole(userId, projectId, RoleType.Viewer);

        _roleRepoMock.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);

        var command = new RemoveUserCommand(projectId, userId, currentUserId);
        await _useCase.ExecuteAsync(command);

        _roleRepoMock.Verify(r => r.Remove(roleEntry), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LastAdmin_ThrowsConflictException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var roleEntry = new UserProjectRole(userId, projectId, RoleType.Admin);

        _roleRepoMock.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _roleRepoMock.Setup(r => r.CountAdminsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new RemoveUserCommand(projectId, userId, currentUserId);
        Func<Task> act = () => _useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExecuteAsync_Self_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        var command = new RemoveUserCommand(Guid.NewGuid(), userId, userId);
        Func<Task> act = () => _useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<SmkDoc.Domain.Exceptions.ValidationException>();
    }
}
