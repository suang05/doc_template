using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using SmkDoc.Domain.Entities;
using SmkDoc.Infrastructure.Security;

namespace SmkDoc.Tests.Infrastructure.Security;

public sealed class JwtTokenGeneratorTests
{
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private JwtTokenGenerator CreateSut(int? accessTokenMinutes = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "valid_secret_key_that_is_at_least_32_bytes_long_12345!",
            ["Jwt:Issuer"] = "SmkDoc",
            ["Jwt:Audience"] = "SmkDoc.Portal"
        };

        if (accessTokenMinutes.HasValue)
        {
            values["Jwt:AccessTokenMinutes"] = accessTokenMinutes.Value.ToString(CultureInfo.InvariantCulture);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new JwtTokenGenerator(configuration, _timeProvider);
    }

    private User CreateUser() => User.Register(
        EmailAddress.Create("alice@example.com"),
        "argon2id-placeholder-hash",
        "Alice",
        "Tester",
        _timeProvider.GetUtcNow(),
        SystemRole.Member);

    [Fact]
    public void GenerateToken_WhenAccessTokenMinutesOmitted_UsesDefault15MinuteLifetime()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateUser();
        var expectedExpiry = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15);

        // Act
        string rawJwt = sut.GenerateToken(user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(rawJwt);

        // Assert
        parsed.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(15)]
    public void GenerateToken_WhenAccessTokenMinutesValid_SetsMatchingExpiry(int minutes)
    {
        // Arrange
        var sut = CreateSut(minutes);
        var user = CreateUser();
        var expectedExpiry = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(minutes);

        // Act
        string rawJwt = sut.GenerateToken(user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(rawJwt);

        // Assert
        parsed.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(60)]
    public void GenerateToken_WhenAccessTokenMinutesOutOfRange_ThrowsInvalidOperationException(int invalidMinutes)
    {
        // Arrange
        var sut = CreateSut(invalidMinutes);
        var user = CreateUser();

        // Act
        var act = () => sut.GenerateToken(user);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:AccessTokenMinutes*");
    }
}
