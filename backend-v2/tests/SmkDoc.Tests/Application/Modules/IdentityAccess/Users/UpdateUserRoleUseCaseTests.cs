using FluentAssertions;
using Moq;
using SmkDoc.Application.UseCases.Users.Commands.UpdateUserRole;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Users;

public class UpdateUserRoleUseCaseTests
{
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly UpdateUserRoleUseCase _useCase;

    public UpdateUserRoleUseCaseTests()
    {
        _useCase = new UpdateUserRoleUseCase(
            _roleRepoMock.Object,
            _uowMock.Object,
            new UpdateUserRoleCommandValidator());
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesRoleSuccessfully()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleEntry = new UserProjectRole(userId, projectId, RoleType.Viewer);

        _roleRepoMock.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);

        var command = new UpdateUserRoleCommand(projectId, userId, "Developer");
        await _useCase.ExecuteAsync(command);

        roleEntry.Role.Should().Be(RoleType.Developer);
        _roleRepoMock.Verify(r => r.Update(roleEntry), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LastAdmin_Demotion_ThrowsConflictException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleEntry = new UserProjectRole(userId, projectId, RoleType.Admin);

        _roleRepoMock.Setup(r => r.GetAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _roleRepoMock.Setup(r => r.CountAdminsAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateUserRoleCommand(projectId, userId, "Viewer");
        Func<Task> act = () => _useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }
}
