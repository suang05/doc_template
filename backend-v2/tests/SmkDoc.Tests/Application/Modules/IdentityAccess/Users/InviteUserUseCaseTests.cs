using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.UseCases.Users.Commands.InviteUser;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Users;

public class InviteUserUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly InviteUserUseCase _useCase;

    public InviteUserUseCaseTests()
    {
        _useCase = new InviteUserUseCase(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _passwordHasherMock.Object,
            _uowMock.Object,
            new InviteUserCommandValidator());
    }

    [Fact]
    public async Task ExecuteAsync_NewUser_CreatesUserAndRole()
    {
        var projectId = Guid.NewGuid();
        var email = "new@test.com";
        var password = "Pass@1234";
        var firstName = "New";
        var lastName = "User";
        var roleStr = "Developer";

        _userRepoMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _passwordHasherMock.Setup(p => p.HashPassword(password)).Returns("hash");

        var command = new InviteUserCommand(projectId, email, password, firstName, lastName, roleStr);
        var result = await _useCase.ExecuteAsync(command);

        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _roleRepoMock.Verify(r => r.AddAsync(It.IsAny<UserProjectRole>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.Email.Should().Be(email);
        result.Role.Should().Be("Developer");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRole_ThrowsValidationException()
    {
        var command = new InviteUserCommand(Guid.NewGuid(), "x@test.com", "Pass@1234", "", "", "SuperAdmin");
        Func<Task> act = () => _useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<SmkDoc.Domain.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task ExecuteAsync_ExistingMember_ThrowsConflictException()
    {
        var projectId = Guid.NewGuid();
        var existingUser = new User("exist@test.com", "hash", "", "");
        var existingRole = new UserProjectRole(existingUser.Id, projectId, RoleType.Viewer);

        _userRepoMock.Setup(r => r.GetByEmailAsync("exist@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _roleRepoMock.Setup(r => r.GetAsync(projectId, existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole);

        var command = new InviteUserCommand(projectId, "exist@test.com", "Pass@1234", "", "", "Viewer");
        Func<Task> act = () => _useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }
}
