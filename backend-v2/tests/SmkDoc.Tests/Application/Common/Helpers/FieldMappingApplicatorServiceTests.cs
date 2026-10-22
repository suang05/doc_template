using System.Text.Json;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Common.Helpers;

public sealed class FieldMappingApplicatorServiceTests
{
    private readonly ISqlExecutorService _sqlExecutor = Substitute.For<ISqlExecutorService>();
    private readonly IMathExpressionResolver _mathResolver = Substitute.For<IMathExpressionResolver>();

    private FieldMappingApplicatorService CreateSut() =>
        new(_sqlExecutor, _mathResolver);

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
}
