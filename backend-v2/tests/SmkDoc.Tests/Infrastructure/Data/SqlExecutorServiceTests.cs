using SmkDoc.Infrastructure.Data;

namespace SmkDoc.Tests.Infrastructure.Data;

public sealed class SqlExecutorServiceTests
{
    private readonly ISqlConnectionValidator _validator = Substitute.For<ISqlConnectionValidator>();
    private readonly ISqlDriverFactory _driverFactory = Substitute.For<ISqlDriverFactory>();
    private readonly ISqlDriverStrategy _strategy = Substitute.For<ISqlDriverStrategy>();

    public SqlExecutorServiceTests()
    {
        _strategy.ProviderName.Returns("PostgreSQL");
        _driverFactory.GetStrategy("PostgreSQL").Returns(_strategy);
    }

    private SqlExecutorService CreateSut() => new(_validator, _driverFactory);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteQueryAsJsonAsync_WhenProviderNullOrWhitespace_ThrowsDomainValidationException(string? provider)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteQueryAsJsonAsync(provider!, "Host=db;Database=test", "SELECT 1", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*provider is required*");
        await _validator.DidNotReceive().ValidateConnectionTargetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteQueryAsJsonAsync_ValidatesTargetAndSqlBeforeDispatch()
    {
        // Arrange
        var sut = CreateSut();
        const string provider = "PostgreSQL";
        const string connStr = "Host=remote.db;Database=prod";
        const string sql = "SELECT id, name FROM users";
        var dict = new Dictionary<string, object> { ["status"] = "active" };
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _strategy.ExecuteQueryAsJsonAsync(connStr, sql, dict, 15, ct)
            .Returns("[{\"id\":1,\"name\":\"Alice\"}]");

        // Act
        var result = await sut.ExecuteQueryAsJsonAsync(provider, connStr, sql, dict, ct);

        // Assert
        result.Should().Be("[{\"id\":1,\"name\":\"Alice\"}]");

        await _validator.Received(1).ValidateConnectionTargetAsync(provider, connStr, ct);
        _validator.Received(1).ValidateReadOnlySql(sql);
        _driverFactory.Received(1).GetStrategy(provider);
        await _strategy.Received(1).ExecuteQueryAsJsonAsync(connStr, sql, dict, 15, ct);
    }

    [Fact]
    public async Task ExecuteQueryAsJsonAsync_WhenTargetValidationFails_DoesNotInvokeStrategy()
    {
        // Arrange
        var sut = CreateSut();
        _validator.ValidateConnectionTargetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ForbiddenException("SSRF blocked"));

        // Act
        var act = () => sut.ExecuteQueryAsJsonAsync("PostgreSQL", "Host=localhost;Database=prod", "SELECT 1", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _driverFactory.DidNotReceive().GetStrategy(Arg.Any<string>());
        await _strategy.DidNotReceive().ExecuteQueryAsJsonAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteQueryAsJsonAsync_WhenReadOnlySqlValidationFails_DoesNotInvokeStrategy()
    {
        // Arrange
        var sut = CreateSut();
        _validator.When(v => v.ValidateReadOnlySql("DROP TABLE users"))
            .Do(_ => throw new DomainValidationException("Forbidden SQL token"));

        // Act
        var act = () => sut.ExecuteQueryAsJsonAsync("PostgreSQL", "Host=remote.db;Database=prod", "DROP TABLE users", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
        _driverFactory.DidNotReceive().GetStrategy(Arg.Any<string>());
        await _strategy.DidNotReceive().ExecuteQueryAsJsonAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}

