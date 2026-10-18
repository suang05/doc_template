using System.Text.Json;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Application.Common.Helpers;

public class FieldMappingApplicatorServiceTests
{
    private readonly Mock<ISqlExecutorService> _sqlExecutorMock = new();
    private readonly Mock<IMathExpressionResolver> _mathResolverMock = new();

    private FieldMappingApplicatorService CreateSut() =>
        new(_sqlExecutorMock.Object, _mathResolverMock.Object);

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

        var now = DateTimeOffset.UtcNow;
        var mapping = FieldMapping.Create(
            templateId: Guid.NewGuid(),
            placeholder: "customer_name",
            sourcePath: string.Empty,
            label: "Customer Name",
            required: false,
            sortOrder: 1,
            now: now,
            dataSourceType: DataSourceType.Sql,
            defaultValue: null,
            transform: null);

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

        _sqlExecutorMock
            .Setup(s => s.ExecuteQueryAsJsonAsync(
                "postgresql",
                "Host=localhost;Database=test",
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IDictionary<string, object>?, CancellationToken>(
                (provider, connStr, sql, parameters, ct) =>
                {
                    capturedQuery = sql;
                    capturedParams = parameters;
                })
            .ReturnsAsync("[{\"id\":\"123\",\"name\":\"Acme Corp\"}]");

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
        var now = DateTimeOffset.UtcNow;

        var mapping = FieldMapping.Create(
            templateId: Guid.NewGuid(),
            placeholder: "recipient_name",
            sourcePath: "name",
            label: "Recipient",
            required: true,
            sortOrder: 1,
            now: now,
            dataSourceType: DataSourceType.Json);

        // Act
        var resultJson = await sut.ApplyAsync(rootJson, [mapping], new Dictionary<string, ResolvedDatasetContext>());

        // Assert
        resultJson.Should().Contain("\"recipient_name\":\"John Doe\"");
    }
}
