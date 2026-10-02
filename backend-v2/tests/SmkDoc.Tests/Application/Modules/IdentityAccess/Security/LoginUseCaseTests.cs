using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security;

public class LoginUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IProjectRepository> _projectRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGeneratorMock = new();
    private readonly LoginUseCase _useCase;

    public LoginUseCaseTests()
    {
        _useCase = new LoginUseCase(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _projectRepoMock.Object,
            _passwordHasherMock.Object,
            _jwtGeneratorMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentialsAndRole_ReturnsTokenAndProjects()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var request = new LoginRequest { Email = "test@example.com", Password = "password123", ProjectId = projectId };
        var user = new User("test@example.com", "hashed_pw", "Test", "User", SystemRole.Member) { Id = Guid.NewGuid() };
        var role = new UserProjectRole(user.Id, projectId, RoleType.Viewer);
        var project = new Project(Guid.NewGuid(), "Project Alpha", "project-alpha") { Id = projectId };

        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _roleRepoMock.Setup(r => r.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProjectRole> { role });

        _projectRepoMock.Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { project });

        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);

        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, projectId, It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("valid_token");
        result.Token.Should().Be("valid_token");
        result.AccessibleProjects.Should().ContainSingle();
        result.DefaultProjectId.Should().Be(projectId);
        result.User.SystemRole.Should().Be("Member");
    }

    [Fact]
    public async Task ExecuteAsync_AsSuperAdmin_ReturnsAllProjectsWithAdminRole()
    {
        // Arrange
        var request = new LoginRequest { Email = "admin@example.com", Password = "admin_password" };
        var user = new User("admin@example.com", "hashed_pw", "Super", "Admin", SystemRole.SuperAdmin) { Id = Guid.NewGuid() };
        var p1 = new Project(Guid.NewGuid(), "ERP", "erp") { Id = Guid.NewGuid() };
        var p2 = new Project(Guid.NewGuid(), "CRM", "crm") { Id = Guid.NewGuid() };

        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(p => p.VerifyPassword("admin_password", "hashed_pw"))
            .Returns(true);

        _projectRepoMock.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { p1, p2 });

        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, p1.Id, It.IsAny<IEnumerable<string>>()))
            .Returns("superadmin_token");

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("superadmin_token");
        result.AccessibleProjects.Should().HaveCount(2);
        result.AccessibleProjects.Should().AllSatisfy(p => p.Role.Should().Be("Admin"));
        result.DefaultProjectId.Should().Be(p1.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPassword_ThrowsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "wrong_password" };
        var user = new User("test@example.com", "hashed_pw", "", "") { Id = Guid.NewGuid() };

        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(p => p.VerifyPassword("wrong_password", "hashed_pw"))
            .Returns(false);

        // Act & Assert
        Func<Task> act = () => _useCase.ExecuteAsync(request);
        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
