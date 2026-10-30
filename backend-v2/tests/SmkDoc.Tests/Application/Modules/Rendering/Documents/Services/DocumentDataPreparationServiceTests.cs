using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentDataPreparationServiceTests
{
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();
    private readonly IFieldMappingApplicatorService _applicator = Substitute.For<IFieldMappingApplicatorService>();
    private readonly IJsonSchemaValidationService _schemaValidation = Substitute.For<IJsonSchemaValidationService>();

    private DocumentDataPreparationService CreateSut() => new(
        _datasetRepo,
        _connectionRepo,
        _dataProtection,
        _applicator,
        _schemaValidation);

    [Fact]
    public async Task PrepareDataAsync_WhenNoMappings_ShouldReturnRawJson()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Tpl").WithSlug("tpl").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tpl.html");

        using var jsonDoc = JsonDocument.Parse("""{"key":"value"}""");

        // Act
        var result = await CreateSut().PrepareDataAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        result.DataJson.Should().Be("""{"key":"value"}""");
        result.ValidationResult.Should().BeNull();
        await _applicator.DidNotReceive().ApplyAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<IEnumerable<FieldMapping>>(),
            Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepareDataAsync_WhenValidationFails_ShouldReturnFailedValidationResult()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Tpl").WithSlug("tpl").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tpl.html");
        const string schema = """{"required":["field"]}""";
        version.UpdateDataSchema(schema, null, TestConstants.BaselineTime);

        var errors = new List<ValidationErrorItem> { new("/", "required", "Missing field") };
        _schemaValidation.Validate(schema, Arg.Any<string>())
            .Returns(SchemaValidationResult.Failure(errors));

        using var jsonDoc = JsonDocument.Parse("""{}""");

        // Act
        var result = await CreateSut().PrepareDataAsync(template, version, jsonDoc.RootElement, skipValidation: false);

        // Assert
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.IsValid.Should().BeFalse();
        result.ValidationResult.Errors.Should().HaveCount(1);
    }
}
