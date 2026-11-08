using System.Text.Json;
using SmkDoc.Application.Modules.DataBinding.DTOs;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentDataPreparationServiceTests
{
    private readonly IDataBindingEngine _dataBindingEngine = Substitute.For<IDataBindingEngine>();

    private DocumentDataPreparationService CreateSut() => new(_dataBindingEngine);

    [Fact]
    public async Task PrepareDataAsync_WhenPayloadProvided_DelegatesToDataBindingEngine()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Tpl").WithSlug("tpl").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tpl.html");

        using var jsonDoc = JsonDocument.Parse("""{"key":"value","nested":{"count":42}}""");

        _dataBindingEngine.BindAndValidateAsync(template, version, Arg.Any<JsonElement>(), true, Arg.Any<CancellationToken>())
            .Returns(new BoundDocumentData("""{"key":"value","nested":{"count":42}}""", null));

        // Act
        var result = await CreateSut().PrepareDataAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        result.DataJson.Should().Be("""{"key":"value","nested":{"count":42}}""");
        result.ValidationResult.Should().BeNull();
    }

    [Fact]
    public async Task PrepareDataAsync_WhenValidationFails_ReturnsFailedValidationResult()
    {
        // Arrange
        var template = new TemplateBuilder().WithName("Tpl").WithSlug("tpl").Build();
        var version = TemplateVersionBuilder.PublishedHtml(templateId: template.Id, version: 1, storageKey: "tpl.html");

        var errors = new List<ValidationErrorItem> { new("/field", "required", "Missing required property: field") };
        _dataBindingEngine.BindAndValidateAsync(template, version, Arg.Any<JsonElement>(), false, Arg.Any<CancellationToken>())
            .Returns(new BoundDocumentData("{}", SchemaValidationResult.Failure(errors)));

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
