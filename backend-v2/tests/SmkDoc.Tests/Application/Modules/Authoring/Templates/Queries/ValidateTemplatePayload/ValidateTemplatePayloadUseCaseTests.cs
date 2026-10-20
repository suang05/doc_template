using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

public class ValidateTemplatePayloadUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();
    private readonly Mock<IJsonSchemaValidationService> _mockSchemaValidation = new();
    private readonly Mock<IExecutionContext> _mockContext = new();

    private ValidateTemplatePayloadUseCase CreateSut() => new(
        _mockTemplateRepo.Object,
        _mockSchemaValidation.Object,
        _mockContext.Object);

    private (Template Template, TemplateVersion Version) GivenTemplateWithSchema(
        string slug,
        int versionNumber,
        string? schema,
        string name = "Invoice TH")
    {
        var projectId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(projectId)
            .WithName(name)
            .WithSlug(slug)
            .Build();
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        var version = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(versionNumber)
            .WithStorageKey($"templates/{slug}.html")
            .WithFormat(TemplateFormat.Html)
            .WithStatus("Published")
            .WithCommitMessage($"v{versionNumber}")
            .Build();

        if (schema != null)
        {
            version.UpdateDataSchema(schema, "{}", TestConstants.BaselineTime);
        }

        _mockContext.Setup(c => c.ProjectId).Returns(projectId);
        _mockTemplateRepo
            .Setup(r => r.GetBySlugAsync(slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockTemplateRepo
            .Setup(r => r.GetVersionByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        return (template, version);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadValid_ReturnsSuccess()
    {
        // Arrange
        const string slug = "invoice-th";
        const string schema = """{"type":"object","required":["doc_no"]}""";
        GivenTemplateWithSchema(slug, 1, schema);

        _mockSchemaValidation
            .Setup(s => s.Validate(schema, It.IsAny<JsonElement>()))
            .Returns(SchemaValidationResult.Success());

        using var doc = JsonDocument.Parse("""{"doc_no":"INV-001"}""");
        var command = new ValidateTemplatePayloadQuery(slug, doc.RootElement);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Valid.Should().BeTrue();
        result.TemplateSlug.Should().Be(slug);
        result.Version.Should().Be(1);
        result.Errors.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadInvalid_ReturnsFailureWithErrors()
    {
        // Arrange
        const string slug = "invoice-th";
        const string schema = """{"type":"object","required":["doc_no"]}""";
        GivenTemplateWithSchema(slug, 2, schema);

        var errors = new List<ValidationErrorItem>
        {
            new("/doc_no", "required", "Required property 'doc_no' was not present")
        };
        _mockSchemaValidation
            .Setup(s => s.Validate(schema, It.IsAny<JsonElement>()))
            .Returns(SchemaValidationResult.Failure(errors));

        using var doc = JsonDocument.Parse("""{}""");
        var command = new ValidateTemplatePayloadQuery(slug, doc.RootElement);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Valid.Should().BeFalse();
        result.TemplateSlug.Should().Be(slug);
        result.Version.Should().Be(2);
        result.Errors.Should().NotBeNullOrEmpty();
        result.Errors![0].Field.Should().Be("/doc_no");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateHasNoSchema_ReturnsSuccessByDefault()
    {
        // Arrange
        const string slug = "no-schema-template";
        GivenTemplateWithSchema(slug, 1, null, "No Schema");

        using var doc = JsonDocument.Parse("""{"any":"field"}""");
        var command = new ValidateTemplatePayloadQuery(slug, doc.RootElement);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Valid.Should().BeTrue();
        result.Message.Should().Contain("no defined schema");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ThrowsNotFoundException()
    {
        _mockTemplateRepo
            .Setup(r => r.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        using var doc = JsonDocument.Parse("{}");
        var command = new ValidateTemplatePayloadQuery("missing-template", doc.RootElement);

        var act = async () => await CreateSut().ExecuteAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
