using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateSchema;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries;

public sealed class GetTemplateSchemaUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();

    private GetTemplateSchemaUseCase CreateSut() => new(_templateRepo);

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTemplateSchemaContract_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithSlug("bill-template")
            .WithName("bill-template");
        template.SetCurrentVersion(versionId, TestConstants.BaselineTime);

        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(3)
            .WithFormat(TemplateFormat.Html);
        version.UpdateDataSchema("{\"type\":\"object\"}", "{\"amount\": 500}", TestConstants.BaselineTime);

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);

        var query = new GetTemplateSchemaQuery(templateId);

        // Act
        var result = await CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TemplateId.Should().Be(templateId);
        result.Slug.Should().Be("bill-template");
        result.Format.Should().Be(TemplateFormat.Html);
        result.Version.Should().Be(3);
        result.DataSchema.Should().Be("{\"type\":\"object\"}");
        result.SamplePayload.Should().Be("{\"amount\": 500}");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowNotFoundException_WhenTemplateDoesNotExist()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        var query = new GetTemplateSchemaQuery(templateId);

        // Act
        var act = () => CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
