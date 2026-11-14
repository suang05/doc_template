using SmkDoc.Application.Modules.IdentityAccess.Users.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Services;

public sealed class UserAccountProcessorTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    private UserAccountProcessor CreateSut() =>
        new(_userRepo, _passwordHasher);

    [Fact]
    public async Task GetOrRegisterUserAsync_WhenUserExists_ReturnsExistingUserWithoutHashing()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        User existingUser = aUser.Active().WithEmail("existing@example.com").WithTime(now);

        _userRepo.GetByEmailAsync(Arg.Is<EmailAddress>(e => e.Value == "existing@example.com"), Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        var sut = CreateSut();
        var result = await sut.GetOrRegisterUserAsync("existing@example.com", "Password123!", "John", "Doe", now);

        // Assert
        result.Should().BeSameAs(existingUser);
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _userRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrRegisterUserAsync_WhenUserDoesNotExist_HashesPasswordAndAddsNewUser()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _passwordHasher.HashPassword("SecurePassword123!")
            .Returns("hashed_secure_pw");

        // Act
        var sut = CreateSut();
        var result = await sut.GetOrRegisterUserAsync("newuser@example.com", "SecurePassword123!", "Jane", "Smith", now);

        // Assert
        result.Should().NotBeNull();
        result.Email.Value.Should().Be("newuser@example.com");
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.PasswordHash.Should().Be("hashed_secure_pw");
        await _userRepo.Received(1).AddAsync(Arg.Is<User>(u => u.Email.Value == "newuser@example.com"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public async Task GetOrRegisterUserAsync_WhenPasswordTooShortOrEmpty_ThrowsValidationException(string invalidPassword)
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var sut = CreateSut();
        var act = () => sut.GetOrRegisterUserAsync("newuser@example.com", invalidPassword, "Jane", "Smith", now);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("Password");
        ex.Which.Errors["Password"].Should().Contain("Password must be at least 8 characters.");
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _userRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
