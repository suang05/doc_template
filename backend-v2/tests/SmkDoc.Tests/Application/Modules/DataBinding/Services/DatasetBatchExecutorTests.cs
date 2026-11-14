using System.Text.Json;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.DataBinding.Services;

namespace SmkDoc.Tests.Application.Modules.DataBinding.Services;

public sealed class DatasetBatchExecutorTests
{
    private readonly ISqlExecutorService _sqlExecutor = Substitute.For<ISqlExecutorService>();
    private readonly IDatasetResultCache _datasetCache = Substitute.For<IDatasetResultCache>();

    private DatasetBatchExecutor CreateSut(bool withCache = true) =>
        new(_sqlExecutor, withCache ? _datasetCache : null);

    [Fact]
    public async Task ExecuteBatchAsync_WhenNoAliases_ReturnsEmptyDictionary()
    {
        var sut = CreateSut();
        var emptyAliases = new Dictionary<string, ResolvedDatasetContext>();
        var callerContext = new Dictionary<string, object?>();

        var result = await sut.ExecuteBatchAsync(emptyAliases, callerContext, Guid.NewGuid());

        result.Should().BeEmpty();
        await _sqlExecutor.DidNotReceiveWithAnyArgs().ExecuteQueryAsJsonAsync(
            default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithQueries_ExecutesSqlAndReturnsJsonElements()
    {
        var sut = CreateSut();
        var datasetId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var aliases = new Dictionary<string, ResolvedDatasetContext>
        {
            ["orders"] = new("PostgreSQL", "conn-str", "SELECT * FROM orders WHERE user_id = @userId", datasetId, connectionId, 0)
        };
        var callerContext = new Dictionary<string, object?> { ["userId"] = 12345 };

        _sqlExecutor.ExecuteQueryAsJsonAsync(
            "PostgreSQL",
            "conn-str",
            "SELECT * FROM orders WHERE user_id = @userId",
            Arg.Is<IDictionary<string, object>?>(p => p != null && p.ContainsKey("userId")),
            Arg.Any<CancellationToken>())
            .Returns("""[{"order_id":1001,"total":500}]""");

        var result = await sut.ExecuteBatchAsync(aliases, callerContext, Guid.NewGuid());

        result.Should().ContainKey("orders");
        result["orders"].ValueKind.Should().Be(JsonValueKind.Array);
        result["orders"][0].GetProperty("order_id").GetInt32().Should().Be(1001);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenCached_ReturnsCachedJsonWithoutCallingSql()
    {
        var sut = CreateSut(withCache: true);
        var datasetId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var aliases = new Dictionary<string, ResolvedDatasetContext>
        {
            ["cached_ds"] = new("PostgreSQL", "conn-str", "SELECT 1", datasetId, connectionId, CacheSeconds: 60)
        };
        var callerContext = new Dictionary<string, object?>();

        _datasetCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("""{"cached":true}""");

        var result = await sut.ExecuteBatchAsync(aliases, callerContext, Guid.NewGuid());

        result.Should().ContainKey("cached_ds");
        result["cached_ds"].GetProperty("cached").GetBoolean().Should().BeTrue();
        await _sqlExecutor.DidNotReceiveWithAnyArgs().ExecuteQueryAsJsonAsync(
            default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenCacheMiss_ExecutesSqlAndSetsCache()
    {
        var sut = CreateSut(withCache: true);
        var datasetId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var aliases = new Dictionary<string, ResolvedDatasetContext>
        {
            ["cached_ds"] = new("PostgreSQL", "conn-str", "SELECT 1", datasetId, connectionId, CacheSeconds: 30)
        };
        var callerContext = new Dictionary<string, object?>();

        _datasetCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        _sqlExecutor.ExecuteQueryAsJsonAsync(
            "PostgreSQL",
            "conn-str",
            "SELECT 1",
            Arg.Any<IDictionary<string, object>?>(),
            Arg.Any<CancellationToken>())
            .Returns("""{"fresh":true}""");

        var result = await sut.ExecuteBatchAsync(aliases, callerContext, Guid.NewGuid());

        result.Should().ContainKey("cached_ds");
        await _datasetCache.Received(1).SetAsync(
            Arg.Any<string>(),
            """{"fresh":true}""",
            TimeSpan.FromSeconds(30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteBatchAsync_WhenSqlFails_ThrowsDatasetExecutionException()
    {
        var sut = CreateSut();
        var aliases = new Dictionary<string, ResolvedDatasetContext>
        {
            ["failed_ds"] = new("PostgreSQL", "conn-str", "SELECT invalid", Guid.NewGuid(), Guid.NewGuid(), 0)
        };
        var callerContext = new Dictionary<string, object?>();

        _sqlExecutor.ExecuteQueryAsJsonAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Table not found"));

        var act = () => sut.ExecuteBatchAsync(aliases, callerContext, Guid.NewGuid());

        await act.Should().ThrowAsync<DatasetExecutionException>()
            .WithMessage("*failed_ds*");
    }

    [Fact]
    public void ExtractParameters_ResolvesParametersFromCallerContext()
    {
        const string query = "SELECT * FROM users WHERE id = @userId AND org = @orgId AND unused = @unused";
        using var jsonDoc = JsonDocument.Parse("""{"orgId":"org_999"}""");
        var callerContext = new Dictionary<string, object?>
        {
            ["userId"] = 42,
            ["orgId"] = jsonDoc.RootElement.GetProperty("orgId")
        };

        var parameters = DatasetBatchExecutor.ExtractParameters(query, callerContext);

        parameters.Should().HaveCount(2);
        parameters["userId"].Should().Be(42);
        parameters["orgId"].Should().Be("org_999");
        parameters.Should().NotContainKey("unused");
    }
}
