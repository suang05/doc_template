using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.UseCases.Security;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Security;

public class UserManagementUseCaseTests
{
    private readonly Mock<IRepository<User>> _userRepoMock = new();
    private readonly Mock<IRepository<UserProjectRole>> _roleRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly UserManagementUseCase _useCase;

    public UserManagementUseCaseTests()
    {
        _useCase = new UserManagementUseCase(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _passwordHasherMock.Object,
            _uowMock.Object);
    }

    [Fact]
    public async Task ListAsync_ReturnsUsersInProject()
    {
        var projectId = Guid.NewGuid();
        var user = new User("admin@test.com", "hash", "Admin", "User");
        var userId = user.Id;
        var roles = new List<UserProjectRole>
        {
            new() { UserId = userId, ProjectId = projectId, Role = RoleType.Admin }
        };
        var users = new List<User> { user };

        _roleRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<UserProjectRole, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);
        _userRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _useCase.ListAsync(projectId);

        result.Should().ContainSingle();
        result[0].Email.Should().Be("admin@test.com");
        result[0].Role.Should().Be("Admin");
    }

    [Fact]
    public async Task InviteAsync_NewUser_CreatesUserAndRole()
    {
        var projectId = Guid.NewGuid();
        var email = "new@test.com";
        var password = "Pass@1234";
        var firstName = "New";
        var lastName = "User";
        var roleStr = "Developer";

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _passwordHasherMock.Setup(p => p.HashPassword("Pass@1234")).Returns("hash");

        var result = await _useCase.InviteAsync(projectId, email, password, firstName, lastName, roleStr);

        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _roleRepoMock.Verify(r => r.AddAsync(It.IsAny<UserProjectRole>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Email.Should().Be("new@test.com");
        result.Role.Should().Be("Developer");
    }

    [Fact]
    public async Task InviteAsync_InvalidRole_ThrowsArgumentException()
    {
        var act = () => _useCase.InviteAsync(Guid.NewGuid(), "x@test.com", "Pass@1234", "", "", "SuperAdmin");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InviteAsync_ExistingMember_ThrowsInvalidOperation()
    {
        var projectId = Guid.NewGuid();
        var existingUser = new User("exist@test.com", "hash", "", "");
        var existingRole = new UserProjectRole { UserId = existingUser.Id, ProjectId = projectId, Role = RoleType.Viewer };
        var email = "exist@test.com";
        var password = "Pass@1234";
        var roleStr = "Viewer";

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        _roleRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserProjectRole, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole);

        var act = () => _useCase.InviteAsync(projectId, email, password, "", "", roleStr);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RemoveAsync_LastAdmin_ThrowsInvalidOperation()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var roleEntry = new UserProjectRole { UserId = userId, ProjectId = projectId, Role = RoleType.Admin };
        var admins = new List<UserProjectRole> { roleEntry };

        _roleRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserProjectRole, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _roleRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<UserProjectRole, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(admins);

        var act = () => _useCase.RemoveAsync(projectId, userId, currentUserId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RemoveAsync_Self_ThrowsInvalidOperation()
    {
        var userId = Guid.NewGuid();
        var act = () => _useCase.RemoveAsync(Guid.NewGuid(), userId, userId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateRoleAsync_LastAdmin_Demotion_ThrowsInvalidOperation()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleEntry = new UserProjectRole { UserId = userId, ProjectId = projectId, Role = RoleType.Admin };
        var admins = new List<UserProjectRole> { roleEntry };

        _roleRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserProjectRole, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleEntry);
        _roleRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<UserProjectRole, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(admins);

        var act = () => _useCase.UpdateRoleAsync(projectId, userId, "Viewer");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
