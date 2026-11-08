using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.DataBinding.Services;
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
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();
    private readonly IFieldMappingApplicatorService _fieldMappingApplicator = Substitute.For<IFieldMappingApplicatorService>();
    private readonly IJsonSchemaValidationService _schemaValidation = Substitute.For<IJsonSchemaValidationService>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private DataBindingEngine CreateSut() => new(
        _datasetRepo,
        _connectionRepo,
        _dataProtection,
        _fieldMappingApplicator,
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
        await _fieldMappingApplicator.DidNotReceive().ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<IEnumerable<FieldMapping>>(),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BindAndValidateAsync_WhenTemplateHasMappings_AppliesMappingsThroughApplicator()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Receipt").WithSlug("rcpt").Build();
        var mapping = FieldMapping.Create(
            template.Id,
            "CustomerName",
            "$.customer.name",
            "Customer Name",
            true,
            1,
            TestConstants.BaselineTime);
        template.AddFieldMapping(mapping, TestConstants.BaselineTime);

        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "rcpt.html");
        using var jsonDoc = JsonDocument.Parse("""{"customer":{"name":"Alice"}}""");

        _fieldMappingApplicator.ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<IEnumerable<FieldMapping>>(),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>())
            .Returns("{\"CustomerName\":\"Alice\"}");

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        result.DataJson.Should().Be("{\"CustomerName\":\"Alice\"}");
        await _fieldMappingApplicator.Received(1).ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Is<IEnumerable<FieldMapping>>(m => m.Any(x => x.Placeholder == "CustomerName")),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>());
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
            "$.tax_number",
            "Tax ID",
            true,
            1,
            TestConstants.BaselineTime);

        version.UpdateMappingsSnapshot(
            FieldMappingSnapshotSerializer.Serialize([snapshotMapping]),
            TestConstants.BaselineTime);

        using var jsonDoc = JsonDocument.Parse("""{"tax_number":"1234567890"}""");

        _fieldMappingApplicator.ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<IEnumerable<FieldMapping>>(),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>())
            .Returns("{\"TaxId\":\"1234567890\"}");

        // Act
        var result = await CreateSut().BindAndValidateAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        result.DataJson.Should().Be("{\"TaxId\":\"1234567890\"}");
        await _fieldMappingApplicator.Received(1).ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Is<IEnumerable<FieldMapping>>(m => m.Any(x => x.Placeholder == "TaxId")),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>());
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
}

