using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.DataBinding;

public sealed class DataBindingEngineTests
{
    private readonly IDatasetResolver _datasetResolver = Substitute.For<IDatasetResolver>();
    private readonly IDatasetBatchExecutor _datasetExecutor = Substitute.For<IDatasetBatchExecutor>();
    private readonly IMathExpressionResolver _mathResolver = Substitute.For<IMathExpressionResolver>();
    private readonly IJsonSchemaValidationService _schemaValidation = Substitute.For<IJsonSchemaValidationService>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private DataBindingEngine CreateSut() => new(
        _datasetResolver,
        _datasetExecutor,
        _mathResolver,
        _schemaValidation,
        _timeProvider);

    [Fact]
    public async Task BindAndValidateAsync_WhenNoMappingsAndNoDatasets_PassesThroughRawPayload()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Invoice").WithSlug("inv").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "inv.html");
        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME Corp","amount":1500}""");

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        result.DataJson.Should().Be("""{"customer":"ACME Corp","amount":1500}""");
        result.ValidationResult.Should().BeNull();
        await _datasetExecutor.DidNotReceiveWithAnyArgs().ExecuteBatchAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task BindAndValidateAsync_WhenTemplateHasJsonMappings_AppliesMappingsDirectly()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Receipt").WithSlug("rcpt").Build();
        var mapping = FieldMapping.Create(
            template.Id,
            "CustomerName",
            "customer.name",
            "Customer Name",
            true,
            1,
            TestConstants.BaselineTime);
        template.AddFieldMapping(mapping, TestConstants.BaselineTime);

        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "rcpt.html");
        using var jsonDoc = JsonDocument.Parse("""{"customer":{"name":"Alice"}}""");

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        using var parsedOutput = JsonDocument.Parse(result.DataJson);
        parsedOutput.RootElement.GetProperty("CustomerName").GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task BindAndValidateAsync_WhenTemplateMappingsEmpty_FallsBackToVersionMappingsSnapshot()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("TaxDoc").WithSlug("tax-doc").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tax.html");

        var snapshotMapping = FieldMapping.Create(
            template.Id,
            "TaxId",
            "tax_number",
            "Tax ID",
            true,
            1,
            TestConstants.BaselineTime);

        version.UpdateMappingsSnapshot(
            FieldMappingSnapshotSerializer.Serialize([snapshotMapping]),
            TestConstants.BaselineTime);

        using var jsonDoc = JsonDocument.Parse("""{"tax_number":"1234567890"}""");

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        using var parsedOutput = JsonDocument.Parse(result.DataJson);
        parsedOutput.RootElement.GetProperty("TaxId").GetString().Should().Be("1234567890");
    }

    [Fact]
    public async Task BindAndValidateAsync_WhenSchemaValidationFails_ReturnsFailureValidationResult()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("StrictDoc").WithSlug("strict").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "strict.html");
        const string schema = """{"required":["mandatory_field"]}""";
        version.UpdateDataSchema(schema, null, TestConstants.BaselineTime);

        var errors = new List<ValidationErrorItem>
        {
            new("/mandatory_field", "required", "Missing required property: mandatory_field")
        };

        _schemaValidation.Validate(schema, Arg.Any<string>())
            .Returns(SchemaValidationResult.Failure(errors));

        using var jsonDoc = JsonDocument.Parse("""{}""");

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: false);

        // Assert
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.IsValid.Should().BeFalse();
        result.ValidationResult.Errors.Should().HaveCount(1);
        result.ValidationResult.Errors[0].Field.Should().Be("/mandatory_field");
    }

    [Fact]
    public async Task BindAndValidateAsync_WhenTemplateHasDatasets_ResolvesAndExecutesDatasets()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Invoice").WithSlug("inv").Build();
        var td = new TemplateDatasetBuilder().WithTemplateId(template.Id.Value).WithDatasetId(Guid.NewGuid()).WithAlias("ds1").Build();
        template.ReplaceDatasets([td], TestConstants.BaselineTime);

        var mapping = FieldMapping.Create(
            template.Id,
            "TotalDue",
            "ds1",
            "Total Due",
            true,
            1,
            TestConstants.BaselineTime);
        mapping.ConfigureDataSource(
            DataSourceType.Sql,
            SmkDoc.Domain.ValueObjects.DatasetAlias.Create("ds1"),
            "total",
            null,
            TestConstants.BaselineTime);
        template.AddFieldMapping(mapping, TestConstants.BaselineTime);

        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "inv.html");
        using var jsonDoc = JsonDocument.Parse("{}");

        var resolvedContexts = new Dictionary<string, ResolvedDatasetContext>
        {
            ["ds1"] = new("PostgreSQL", "conn", "SELECT 1", Guid.NewGuid(), Guid.NewGuid(), 0)
        };

        _datasetResolver.ResolveAssignmentsAsync(Arg.Any<IEnumerable<TemplateDataset>>(), Arg.Any<CancellationToken>())
            .Returns(resolvedContexts);

        using var datasetResultDoc = JsonDocument.Parse("""[{"total":999.50}]""");
        _datasetExecutor.ExecuteBatchAsync(
            resolvedContexts,
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            template.ProjectId.Value,
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["ds1"] = datasetResultDoc.RootElement.Clone()
            });

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        await _datasetResolver.Received(1).ResolveAssignmentsAsync(Arg.Any<IEnumerable<TemplateDataset>>(), Arg.Any<CancellationToken>());
        await _datasetExecutor.Received(1).ExecuteBatchAsync(
            resolvedContexts,
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            template.ProjectId.Value,
            Arg.Any<CancellationToken>());

        using var parsedOutput = JsonDocument.Parse(result.DataJson);
        parsedOutput.RootElement.GetProperty("TotalDue").GetString().Should().Be("999.50");
    }

    [Fact]
    public async Task BindAndValidateAsync_WithMathExpression_ResolvesEvaluatedExpression()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("TaxDoc").WithSlug("tax").Build();
        var mapping = FieldMapping.Create(
            template.Id,
            "GrossTotal",
            "",
            "Gross Total",
            true,
            1,
            TestConstants.BaselineTime);
        mapping.ConfigureDataSource(
            DataSourceType.Json,
            null,
            null,
            "NetTotal * 1.07",
            TestConstants.BaselineTime);
        template.AddFieldMapping(mapping, TestConstants.BaselineTime);

        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tax.html");
        using var jsonDoc = JsonDocument.Parse("""{"NetTotal":100}""");

        _mathResolver.Resolve("NetTotal * 1.07", Arg.Any<IReadOnlyDictionary<string, string?>>())
            .Returns("107.00");

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        using var parsedOutput = JsonDocument.Parse(result.DataJson);
        parsedOutput.RootElement.GetProperty("GrossTotal").GetString().Should().Be("107.00");
    }
}
