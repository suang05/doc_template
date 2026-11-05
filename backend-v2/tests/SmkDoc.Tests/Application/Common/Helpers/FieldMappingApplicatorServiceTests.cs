using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Cache;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Common.Helpers;

public sealed class FieldMappingApplicatorServiceTests
{
    private readonly ISqlExecutorService _sqlExecutor = Substitute.For<ISqlExecutorService>();
    private readonly IMathExpressionResolver _mathResolver = Substitute.For<IMathExpressionResolver>();

    private FieldMappingApplicatorService CreateSut(
        IDatasetResultCache? cache = null,
        ICurrentProjectContext? projectContext = null) =>
        new(_sqlExecutor, _mathResolver, cache, projectContext);

    [Fact]
    public async Task ApplyAsync_WithSqlDataSource_ExtractsParametersSafelyWithoutRawInterpolation()
    {
        // Arrange
        var sut = CreateSut();
        const string maliciousInput = "'; DROP TABLE templates; --";
        const string rawSqlQuery = "SELECT id, name FROM customers WHERE customer_id = @customerId AND is_active = @isActive";

        var rootJson = JsonSerializer.Deserialize<JsonElement>($$"""
        {
            "customerId": "{{maliciousInput}}",
            "isActive": true
        }
        """);

        var now = TestConstants.BaselineTime;
        var mapping = new FieldMappingBuilder()
            .WithPlaceholder("customer_name")
            .WithSourcePath(string.Empty)
            .WithLabel("Customer Name")
            .WithRequired(false)
            .WithSortOrder(1)
            .WithTime(now)
            .WithDataSourceType(DataSourceType.Sql)
            .Build();

        mapping.ConfigureDataSource(
            type: DataSourceType.Sql,
            alias: DatasetAlias.Create("customers"),
            resultPath: "0.name",
            mathExpression: null,
            now: now);

        var datasetContext = new ResolvedDatasetContext(
            Provider: "postgresql",
            ConnectionString: "Host=localhost;Database=test",
            SqlQuery: rawSqlQuery);

        var datasetAliases = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase)
        {
            ["customers"] = datasetContext
        };

        IDictionary<string, object>? capturedParams = null;
        string? capturedQuery = null;

        _sqlExecutor
            .ExecuteQueryAsJsonAsync(
                "postgresql",
                "Host=localhost;Database=test",
                Arg.Do<string>(sql => capturedQuery = sql),
                Arg.Do<IDictionary<string, object>?>(parameters => capturedParams = parameters),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("[{\"id\":\"123\",\"name\":\"Acme Corp\"}]"));

        // Act
        var resultJson = await sut.ApplyAsync(rootJson, [mapping], datasetAliases);

        // Assert
        capturedQuery.Should().Be(rawSqlQuery, "Raw SQL query must remain untouched without string interpolation");
        capturedParams.Should().NotBeNull();
        capturedParams!.Should().ContainKey("customerId");
        capturedParams!["customerId"].Should().Be(maliciousInput, "Malicious string must be passed strictly as parameter value");
        capturedParams!.Should().ContainKey("isActive");
        capturedParams!["isActive"].Should().Be(true);

        resultJson.Should().Contain("\"customer_name\":\"Acme Corp\"");
    }

    [Fact]
    public async Task ApplyAsync_WithJsonDataSource_ResolvesAndTransformsCorrectly()
    {
        // Arrange
        var sut = CreateSut();
        var rootJson = JsonSerializer.Deserialize<JsonElement>("{\"name\": \"John Doe\"}");
        var now = TestConstants.BaselineTime;

        var mapping = new FieldMappingBuilder()
            .WithPlaceholder("recipient_name")
            .WithSourcePath("name")
            .WithLabel("Recipient")
            .WithRequired(true)
            .WithSortOrder(1)
            .WithTime(now)
            .WithDataSourceType(DataSourceType.Json)
            .Build();

        // Act
        var resultJson = await sut.ApplyAsync(rootJson, [mapping], new Dictionary<string, ResolvedDatasetContext>());

        // Assert
        resultJson.Should().Contain("\"recipient_name\":\"John Doe\"");
    }

    [Fact]
    public async Task ApplyAsync_WhenSqlExecutionThrows_ThrowsDatasetExecutionException()
    {
        // Arrange
        var sut = CreateSut();
        var rootJson = JsonSerializer.Deserialize<JsonElement>("{\"customerId\": 42}");
        var now = TestConstants.BaselineTime;

        var mapping = new FieldMappingBuilder()
            .WithPlaceholder("customer_name")
            .WithSourcePath(string.Empty)
            .WithLabel("Customer Name")
            .WithRequired(false)
            .WithSortOrder(1)
            .WithTime(now)
            .WithDataSourceType(DataSourceType.Sql)
            .Build();

        mapping.ConfigureDataSource(
            type: DataSourceType.Sql,
            alias: DatasetAlias.Create("customers"),
            resultPath: "0.name",
            mathExpression: null,
            now: now);

        var datasetContext = new ResolvedDatasetContext(
            Provider: "postgresql",
            ConnectionString: "Host=localhost;Database=test",
            SqlQuery: "SELECT name FROM customers WHERE id = @customerId");

        var datasetAliases = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase)
        {
            ["customers"] = datasetContext
        };

        _sqlExecutor
            .ExecuteQueryAsJsonAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection timeout to PostgreSQL cluster"));

        // Act
        var act = () => sut.ApplyAsync(rootJson, [mapping], datasetAliases);

        // Assert
        var exception = await act.Should().ThrowAsync<DatasetExecutionException>();
        exception.Which.Alias.Should().Be("customers");
        exception.Which.Provider.Should().Be("postgresql");
        exception.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ApplyAsync_WithTenDistinctSqlAliases_CapsConcurrencyAtFour()
    {
        // Arrange
        var sut = CreateSut();
        var rootJson = JsonSerializer.Deserialize<JsonElement>("{\"tenantId\": 7}");
        var now = TestConstants.BaselineTime;

        var mappings = new List<SmkDoc.Domain.Entities.FieldMapping>();
        var aliases = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i <= 10; i++)
        {
            string aliasName = $"ds_{i}";
            var mapping = new FieldMappingBuilder()
                .WithPlaceholder($"val_{i}")
                .WithSourcePath(string.Empty)
                .WithLabel($"Value {i}")
                .WithSortOrder(i)
                .WithTime(now)
                .WithDataSourceType(DataSourceType.Sql)
                .Build();

            mapping.ConfigureDataSource(
                DataSourceType.Sql,
                DatasetAlias.Create(aliasName),
                "0.v",
                null,
                now);

            mappings.Add(mapping);
            aliases[aliasName] = new ResolvedDatasetContext(
                Provider: "postgresql",
                ConnectionString: "Host=localhost",
                SqlQuery: $"SELECT {i} AS v WHERE @tenantId = 7",
                DatasetId: Guid.NewGuid());
        }

        int activeCount = 0;
        int maxObservedConcurrency = 0;

        _sqlExecutor.ExecuteQueryAsJsonAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                int current = Interlocked.Increment(ref activeCount);
                while (true)
                {
                    int snapshot = Volatile.Read(ref maxObservedConcurrency);
                    if (current <= snapshot ||
                        Interlocked.CompareExchange(ref maxObservedConcurrency, current, snapshot) == snapshot)
                    {
                        break;
                    }
                }

                try
                {
                    var sql = callInfo.ArgAt<string>(2);
                    var token = callInfo.ArgAt<CancellationToken>(4);
                    await Task.Delay(50, token);
                    return $"[{{\"v\":\"{sql.Split(' ')[1]}\"}}]";
                }
                finally
                {
                    Interlocked.Decrement(ref activeCount);
                }
            });

        // Act
        var resultJson = await sut.ApplyAsync(rootJson, mappings, aliases);

        // Assert
        maxObservedConcurrency.Should().BeGreaterThan(1, "distinct dataset aliases must execute concurrently");
        maxObservedConcurrency.Should().BeLessThanOrEqualTo(
            FieldMappingApplicatorService.MaxConcurrentDatasetQueries,
            "SemaphoreSlim(4) must cap concurrent external SQL queries per render");

        for (int i = 1; i <= 10; i++)
        {
            resultJson.Should().Contain($"\"val_{i}\":\"{i}\"");
        }
    }

    [Fact]
    public async Task ApplyAsync_WhenOneSqlAliasFails_CancelsSiblingInFlightQueries()
    {
        // Arrange
        var sut = CreateSut();
        var rootJson = JsonSerializer.Deserialize<JsonElement>("{\"id\": 1}");
        var now = TestConstants.BaselineTime;

        var fastFailMapping = new FieldMappingBuilder()
            .WithPlaceholder("fail_field")
            .WithSortOrder(1)
            .WithTime(now)
            .Build();
        fastFailMapping.ConfigureDataSource(DataSourceType.Sql, DatasetAlias.Create("failing_ds"), "0.v", null, now);

        var slowMapping = new FieldMappingBuilder()
            .WithPlaceholder("slow_field")
            .WithSortOrder(2)
            .WithTime(now)
            .Build();
        slowMapping.ConfigureDataSource(DataSourceType.Sql, DatasetAlias.Create("slow_ds"), "0.v", null, now);

        var aliases = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase)
        {
            ["failing_ds"] = new("postgresql", "Host=pg", "SELECT 'boom'", Guid.NewGuid()),
            ["slow_ds"] = new("postgresql", "Host=pg", "SELECT pg_sleep(10)", Guid.NewGuid())
        };

        bool siblingObservedCancellation = false;

        _sqlExecutor.ExecuteQueryAsJsonAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var sql = callInfo.ArgAt<string>(2);
                var ct = callInfo.ArgAt<CancellationToken>(4);
                if (sql.Contains("boom", StringComparison.Ordinal))
                {
                    await Task.Delay(15, ct);
                    throw new InvalidOperationException("Simulated SQL failure");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                    return "[{\"v\":1}]";
                }
                catch (OperationCanceledException)
                {
                    siblingObservedCancellation = true;
                    throw;
                }
            });

        // Act
        var act = () => sut.ApplyAsync(rootJson, [fastFailMapping, slowMapping], aliases);

        // Assert
        var ex = await act.Should().ThrowAsync<DatasetExecutionException>();
        ex.Which.Alias.Should().Be("failing_ds");
        siblingObservedCancellation.Should().BeTrue("linked CancellationTokenSource must cancel slow sibling SQL queries immediately");
    }

    [Fact]
    public async Task ApplyAsync_WhenCacheSecondsConfigured_UsesTenantIsolatedCacheAndRespectsZeroTtlBypass()
    {
        // Arrange
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var resultCache = new MemoryDatasetResultCache(memoryCache);

        var tenantA = ProjectId.New();
        var tenantB = ProjectId.New();
        var contextA = Substitute.For<ICurrentProjectContext>();
        contextA.IsResolved.Returns(true);
        contextA.ProjectId.Returns(tenantA);

        var contextB = Substitute.For<ICurrentProjectContext>();
        contextB.IsResolved.Returns(true);
        contextB.ProjectId.Returns(tenantB);

        var rootJson = JsonSerializer.Deserialize<JsonElement>("{\"customerId\": 99}");
        var now = TestConstants.BaselineTime;
        var datasetId = Guid.NewGuid();

        var mapping = new FieldMappingBuilder()
            .WithPlaceholder("customer_name")
            .WithSortOrder(1)
            .WithTime(now)
            .Build();
        mapping.ConfigureDataSource(DataSourceType.Sql, DatasetAlias.Create("customers"), "0.name", null, now);

        var cachedAliases = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase)
        {
            ["customers"] = new(
                Provider: "postgresql",
                ConnectionString: "Host=pg",
                SqlQuery: "SELECT name FROM customers WHERE id = @customerId",
                DatasetId: datasetId,
                DataConnectionId: Guid.NewGuid(),
                CacheSeconds: 60)
        };

        _sqlExecutor.ExecuteQueryAsJsonAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns("[{\"name\":\"Cached Corp\"}]");

        var sutTenantA = CreateSut(resultCache, contextA);
        var sutTenantB = CreateSut(resultCache, contextB);

        // Act — two calls under Tenant A (second must hit cache), one call under Tenant B (must miss Tenant A's cache)
        await sutTenantA.ApplyAsync(rootJson, [mapping], cachedAliases);
        await sutTenantA.ApplyAsync(rootJson, [mapping], cachedAliases);
        await sutTenantB.ApplyAsync(rootJson, [mapping], cachedAliases);

        // Assert
        await _sqlExecutor.Received(2).ExecuteQueryAsJsonAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>?>(),
            Arg.Any<CancellationToken>());

        // Zero-TTL dataset must bypass cache on every execution
        var uncachedAliases = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase)
        {
            ["customers"] = cachedAliases["customers"] with { CacheSeconds = 0 }
        };

        await sutTenantA.ApplyAsync(rootJson, [mapping], uncachedAliases);
        await sutTenantA.ApplyAsync(rootJson, [mapping], uncachedAliases);

        await _sqlExecutor.Received(4).ExecuteQueryAsJsonAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_WithTabularSqlDataset_PreservesArrayAndAutoBindsAlias()
    {
        // Arrange
        var sut = CreateSut();
        var rootJson = JsonSerializer.Deserialize<JsonElement>("{\"invoiceId\":101}");

        var datasetResultJson = "[{\"sku\":\"SKU-01\",\"qty\":2,\"price\":100.5},{\"sku\":\"SKU-02\",\"qty\":1,\"price\":50.0}]";

        _sqlExecutor.ExecuteQueryAsJsonAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(datasetResultJson);

        var aliases = new Dictionary<string, ResolvedDatasetContext>(StringComparer.OrdinalIgnoreCase)
        {
            ["lineItems"] = new(
                Provider: "PostgreSQL",
                ConnectionString: "Host=localhost",
                SqlQuery: "SELECT sku, qty, price FROM items WHERE invoice_id = @invoiceId",
                CacheSeconds: 0)
        };

        var mapping = new FieldMappingBuilder()
            .WithPlaceholder("items")
            .WithDatasetAlias("lineItems")
            .WithSourcePath(string.Empty)
            .WithLabel("Items")
            .WithRequired(false)
            .WithSortOrder(1)
            .WithDataSourceType(DataSourceType.Sql)
            .Build();

        // Act
        var resultJson = await sut.ApplyAsync(rootJson, [mapping], aliases);

        // Assert
        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;

        // 1. Caller parameters preserved
        root.GetProperty("invoiceId").GetInt32().Should().Be(101);

        // 2. Tabular mapping preserved as Array (NOT flattened to scalar)
        root.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("items").GetArrayLength().Should().Be(2);
        root.GetProperty("items")[0].GetProperty("sku").GetString().Should().Be("SKU-01");

        // 3. Dataset alias auto-bound directly
        root.GetProperty("lineitems").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("lineitems").GetArrayLength().Should().Be(2);
    }
}
