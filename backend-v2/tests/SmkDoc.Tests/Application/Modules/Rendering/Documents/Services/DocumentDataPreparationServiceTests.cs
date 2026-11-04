using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentDataPreparationServiceTests
{
    private readonly IJsonSchemaValidationService _schemaValidation = Substitute.For<IJsonSchemaValidationService>();

    private DocumentDataPreparationService CreateSut() => new(_schemaValidation);

    [Fact]
    public async Task PrepareDataAsync_WhenPayloadProvided_ShouldReturnRawJsonWithoutMutation()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Tpl").WithSlug("tpl").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tpl.html");

        using var jsonDoc = JsonDocument.Parse("""{"key":"value","nested":{"count":42}}""");

        // Act
        var result = await CreateSut().PrepareDataAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        result.DataJson.Should().Be("""{"key":"value","nested":{"count":42}}""");
        result.ValidationResult.Should().BeNull();
    }

    [Fact]
    public async Task PrepareDataAsync_WhenValidationFails_ShouldReturnFailedValidationResult()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Tpl").WithSlug("tpl").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tpl.html");
        const string schema = """{"required":["field"]}""";
        version.UpdateDataSchema(schema, null, TestConstants.BaselineTime);

        var errors = new List<ValidationErrorItem> { new("/field", "required", "Missing required property: field") };
        _schemaValidation.Validate(schema, Arg.Any<string>())
            .Returns(SchemaValidationResult.Failure(errors));

        using var jsonDoc = JsonDocument.Parse("""{}""");

        // Act
        var result = await CreateSut().PrepareDataAsync(template, version, jsonDoc.RootElement, skipValidation: false);

        // Assert
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.IsValid.Should().BeFalse();
        result.ValidationResult.Errors.Should().HaveCount(1);
        result.ValidationResult.Errors[0].Field.Should().Be("/field");
        result.ValidationResult.Errors[0].Rule.Should().Be("required");
    }
}
