using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Validation;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Templates;

public class ValidateTemplatePayloadUseCaseTests
{
    private readonly Mock<IRepository<Template>> _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo = new();
    private readonly Mock<IJsonSchemaValidationService> _mockSchemaValidation = new();
    private readonly Mock<IExecutionContext> _mockContext = new();

    private readonly ValidateTemplatePayloadUseCase _useCase;

    public ValidateTemplatePayloadUseCaseTests()
    {
        _useCase = new ValidateTemplatePayloadUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockSchemaValidation.Object,
            _mockContext.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadValid_ReturnsSuccess()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        const string slug = "invoice-th";
        const string schema = """{"type":"object","required":["doc_no"]}""";

        var template = new Template(projectId, "Invoice TH", slug);
        template.SetCurrentVersion(versionId);

        var version = new TemplateVersion(templateId, 1, "templates/invoice.html", TemplateFormat.Html, "Published", "init");
        version.UpdateDataSchema(schema, "{}");

        _mockContext.Setup(c => c.ProjectId).Returns(projectId);
        _mockTemplateRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo
            .Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        _mockSchemaValidation
            .Setup(s => s.Validate(schema, It.IsAny<JsonElement>()))
            .Returns(SchemaValidationResult.Success());

        using var doc = JsonDocument.Parse("""{"doc_no":"INV-001"}""");
        var command = new ValidateTemplatePayloadCommand(slug, doc.RootElement);

        // Act
        var result = await _useCase.ExecuteAsync(command);

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
        var projectId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        const string slug = "invoice-th";
        const string schema = """{"type":"object","required":["doc_no"]}""";

        var template = new Template(projectId, "Invoice TH", slug);
        template.SetCurrentVersion(versionId);

        var version = new TemplateVersion(templateId, 2, "templates/invoice.html", TemplateFormat.Html, "Published", "v2");
        version.UpdateDataSchema(schema, "{}");

        _mockContext.Setup(c => c.ProjectId).Returns(projectId);
        _mockTemplateRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo
            .Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var errors = new List<ValidationErrorItem>
        {
            new("/doc_no", "required", "Required property 'doc_no' was not present")
        };
        _mockSchemaValidation
            .Setup(s => s.Validate(schema, It.IsAny<JsonElement>()))
            .Returns(SchemaValidationResult.Failure(errors));

        using var doc = JsonDocument.Parse("""{}""");
        var command = new ValidateTemplatePayloadCommand(slug, doc.RootElement);

        // Act
        var result = await _useCase.ExecuteAsync(command);

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
        var projectId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        const string slug = "no-schema-template";

        var template = new Template(projectId, "No Schema", slug);
        template.SetCurrentVersion(versionId);

        var version = new TemplateVersion(templateId, 1, "templates/none.html", TemplateFormat.Html, "Published", "init");
        // No schema stored

        _mockTemplateRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo
            .Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        using var doc = JsonDocument.Parse("""{"any":"field"}""");
        var command = new ValidateTemplatePayloadCommand(slug, doc.RootElement);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.Valid.Should().BeTrue();
        result.Message.Should().Contain("no defined schema");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ThrowsNotFoundException()
    {
        _mockTemplateRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        using var doc = JsonDocument.Parse("{}");
        var command = new ValidateTemplatePayloadCommand("missing-template", doc.RootElement);

        var act = async () => await _useCase.ExecuteAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
