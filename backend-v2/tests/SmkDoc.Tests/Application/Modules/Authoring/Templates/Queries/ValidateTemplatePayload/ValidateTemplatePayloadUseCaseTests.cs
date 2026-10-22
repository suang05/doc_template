using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

public sealed class ValidateTemplatePayloadUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IJsonSchemaValidationService _schemaValidation = Substitute.For<IJsonSchemaValidationService>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();

    private ValidateTemplatePayloadUseCase CreateSut() => new(
        _templateRepo,
        _schemaValidation,
        _executionContext);

    private (Template Template, TemplateVersion Version) GivenTemplateWithSchema(
        string slug,
        int versionNumber,
        string? schema,
        string name = "Invoice TH")
    {
        var projectId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(projectId)
            .WithName(name)
            .WithSlug(slug);
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(versionNumber)
            .WithStorageKey($"templates/{slug}.html")
            .WithFormat(TemplateFormat.Html)
            .WithStatus("Published")
            .WithCommitMessage($"v{versionNumber}");

        if (schema != null)
        {
            version.UpdateDataSchema(schema, "{}", TestConstants.BaselineTime);
        }

        _executionContext.ProjectId.Returns(projectId);
        _templateRepo.GetBySlugAsync(slug, Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);

        return (template, version);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadValid_ReturnsSuccess()
    {
        // Arrange
        const string slug = "invoice-th";
        const string schema = """{"type":"object","required":["doc_no"]}""";
        GivenTemplateWithSchema(slug, 1, schema);

        _schemaValidation.Validate(schema, Arg.Any<JsonElement>())
            .Returns(SchemaValidationResult.Success());

        using var doc = JsonDocument.Parse("""{"doc_no":"INV-001"}""");
        var command = new ValidateTemplatePayloadQuery(slug, doc.RootElement);

        // Act
        ValidateTemplatePayloadResult result = await CreateSut().ExecuteAsync(command);

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
        _schemaValidation.Validate(schema, Arg.Any<JsonElement>())
            .Returns(SchemaValidationResult.Failure(errors));

        using var doc = JsonDocument.Parse("""{}""");
        var command = new ValidateTemplatePayloadQuery(slug, doc.RootElement);

        // Act
        ValidateTemplatePayloadResult result = await CreateSut().ExecuteAsync(command);

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
        ValidateTemplatePayloadResult result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Valid.Should().BeTrue();
        result.Message.Should().Contain("no defined schema");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ThrowsNotFoundException()
    {
        _templateRepo.GetBySlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        using var doc = JsonDocument.Parse("{}");
        var command = new ValidateTemplatePayloadQuery("missing-template", doc.RootElement);

        var act = async () => await CreateSut().ExecuteAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
