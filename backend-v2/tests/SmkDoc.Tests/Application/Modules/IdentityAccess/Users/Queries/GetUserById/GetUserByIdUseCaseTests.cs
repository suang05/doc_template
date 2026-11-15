using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetUserById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Users.Queries.GetUserById;

public sealed class GetUserByIdUseCaseTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();

    private GetUserByIdUseCase CreateSut() => new(_userRepo);

    [Fact]
    public async Task ExecuteAsync_WhenUserExists_ReturnsUserWithAggregateVersion()
    {
        // Arrange
        var user = new UserBuilder()
            .WithEmail("global@smkdoc.com")
            .WithName("Narin", "Kiat")
            .Build();
        user.Deactivate(TestConstants.BaselineTime.AddMinutes(10));

        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetUserByIdQuery(user.Id.Value), CancellationToken.None);

        // Assert
        result.Id.Should().Be(user.Id.Value);
        result.Email.Should().Be("global@smkdoc.com");
        result.IsActive.Should().BeFalse();
        result.Role.Should().Be(SystemRole.Member.Name);
        result.Version.Should().Be(user.Version);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserMissing_ThrowsNotFoundException()
    {
        // Arrange
        var userId = UserId.New();
        _userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetUserByIdQuery(userId.Value), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
