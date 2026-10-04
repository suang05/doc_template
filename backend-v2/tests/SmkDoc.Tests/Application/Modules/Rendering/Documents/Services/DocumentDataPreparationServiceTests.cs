using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public class DocumentDataPreparationServiceTests
{
    private readonly Mock<IDatasetRepository> _mockDatasetRepo = new();
    private readonly Mock<IDataConnectionRepository> _mockConnectionRepo = new();
    private readonly Mock<IDataProtectionService> _mockDataProtection = new();
    private readonly Mock<IFieldMappingApplicatorService> _mockApplicator = new();
    private readonly Mock<IJsonSchemaValidationService> _mockSchemaValidation = new();

    private DocumentDataPreparationService BuildService() => new(
        _mockDatasetRepo.Object,
        _mockConnectionRepo.Object,
        _mockDataProtection.Object,
        _mockApplicator.Object,
        _mockSchemaValidation.Object);

    [Fact]
    public async Task PrepareDataAsync_WhenNoMappings_ShouldReturnRawJson()
    {
        // Arrange
        var template = new Template(Guid.NewGuid(), "Tpl", "tpl", null);
        var version = new TemplateVersion(template.Id, 1, "tpl.html", TemplateFormat.Html, "Pub", "Commit");

        var service = BuildService();
        using var jsonDoc = JsonDocument.Parse("""{"key":"value"}""");

        // Act
        var result = await service.PrepareDataAsync(template, version, jsonDoc.RootElement, skipValidation: true);

        // Assert
        result.DataJson.Should().Be("""{"key":"value"}""");
        result.ValidationResult.Should().BeNull();
        _mockApplicator.Verify(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<IEnumerable<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()), Times.Never);
    }

    [Fact]
    public async Task PrepareDataAsync_WhenValidationFails_ShouldReturnFailedValidationResult()
    {
        // Arrange
        var template = new Template(Guid.NewGuid(), "Tpl", "tpl", null);
        var version = new TemplateVersion(template.Id, 1, "tpl.html", TemplateFormat.Html, "Pub", "Commit");
        const string schema = """{"required":["field"]}""";
        version.UpdateDataSchema(schema, null);

        var errors = new List<ValidationErrorItem> { new("/", "required", "Missing field") };
        _mockSchemaValidation.Setup(s => s.Validate(schema, It.IsAny<string>()))
            .Returns(SchemaValidationResult.Failure(errors));

        var service = BuildService();
        using var jsonDoc = JsonDocument.Parse("""{}""");

        // Act
        var result = await service.PrepareDataAsync(template, version, jsonDoc.RootElement, skipValidation: false);

        // Assert
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.IsValid.Should().BeFalse();
        result.ValidationResult.Errors.Should().HaveCount(1);
    }
}
