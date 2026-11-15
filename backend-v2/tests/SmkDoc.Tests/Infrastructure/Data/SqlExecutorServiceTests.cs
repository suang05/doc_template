using System.Net;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Data;

namespace SmkDoc.Tests.Infrastructure.Data;

public sealed class SqlExecutorServiceTests
{
    private readonly IConnectionHostValidator _validator = Substitute.For<IConnectionHostValidator>();
    private readonly ISqlDriverFactory _driverFactory = Substitute.For<ISqlDriverFactory>();
    private readonly ISqlDriverStrategy _strategy = Substitute.For<ISqlDriverStrategy>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public SqlExecutorServiceTests()
    {
        _strategy.ProviderName.Returns("PostgreSQL");
        _driverFactory.GetStrategy("PostgreSQL").Returns(_strategy);
        _projectContext.TryGetProjectId().Returns(_projectId);
    }

    private SqlExecutorService CreateSut() => new(_validator, _driverFactory, _projectContext);

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
        await _validator.DidNotReceive().ValidateConnectionTargetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ProjectId?>(),
            Arg.Any<CancellationToken>());
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
        var pinned = new ResolvedEgressTarget("remote.db", [IPAddress.Parse("93.184.216.34")], 5432);

        _validator.ValidateConnectionTargetAsync(provider, connStr, _projectId, ct)
            .Returns(pinned);
        _strategy.ExecuteQueryWithPinnedTargetAsync(connStr, sql, dict, 15, pinned, ct)
            .Returns("[{\"id\":1,\"name\":\"Alice\"}]");

        // Act
        var result = await sut.ExecuteQueryAsJsonAsync(provider, connStr, sql, dict, ct);

        // Assert
        result.Should().Be("[{\"id\":1,\"name\":\"Alice\"}]");

        await _validator.Received(1).ValidateConnectionTargetAsync(provider, connStr, _projectId, ct);
        _validator.Received(1).ValidateReadOnlySql(sql);
        _driverFactory.Received(1).GetStrategy(provider);
        await _strategy.Received(1).ExecuteQueryWithPinnedTargetAsync(connStr, sql, dict, 15, pinned, ct);
    }

    [Fact]
    public async Task ExecuteQueryAsJsonAsync_WhenTargetValidationFails_DoesNotInvokeStrategy()
    {
        // Arrange
        var sut = CreateSut();
        _validator.ValidateConnectionTargetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ProjectId?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ForbiddenException("SSRF blocked"));

        // Act
        var act = () => sut.ExecuteQueryAsJsonAsync("PostgreSQL", "Host=localhost;Database=prod", "SELECT 1", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _driverFactory.DidNotReceive().GetStrategy(Arg.Any<string>());
        await _strategy.DidNotReceive().ExecuteQueryWithPinnedTargetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>?>(),
            Arg.Any<int>(),
            Arg.Any<ResolvedEgressTarget>(),
            Arg.Any<CancellationToken>());
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
        await _strategy.DidNotReceive().ExecuteQueryWithPinnedTargetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>?>(),
            Arg.Any<int>(),
            Arg.Any<ResolvedEgressTarget>(),
            Arg.Any<CancellationToken>());
    }
}
