using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.UseCases.Security;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using System.Linq.Expressions;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Security;

public class LoginUseCaseTests
{
    private readonly Mock<IRepository<User>> _userRepoMock;
    private readonly Mock<IRepository<UserProjectRole>> _roleRepoMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtTokenGenerator> _jwtGeneratorMock;
    private readonly LoginUseCase _useCase;

    public LoginUseCaseTests()
    {
        _userRepoMock = new Mock<IRepository<User>>();
        _roleRepoMock = new Mock<IRepository<UserProjectRole>>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtGeneratorMock = new Mock<IJwtTokenGenerator>();

        _useCase = new LoginUseCase(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _passwordHasherMock.Object,
            _jwtGeneratorMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentialsAndRole_ReturnsToken()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "password123", ProjectId = Guid.NewGuid() };
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hashed_pw", IsActive = true };
        var role = new UserProjectRole { UserId = user.Id, ProjectId = request.ProjectId, Role = RoleType.Viewer };

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        
        _roleRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserProjectRole, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);

        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, request.ProjectId, It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("valid_token", result.AccessToken);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPassword_ThrowsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@example.com", Password = "wrong_password", ProjectId = Guid.NewGuid() };
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hashed_pw", IsActive = true };

        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(p => p.VerifyPassword("wrong_password", "hashed_pw"))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _useCase.ExecuteAsync(request));
    }
}
