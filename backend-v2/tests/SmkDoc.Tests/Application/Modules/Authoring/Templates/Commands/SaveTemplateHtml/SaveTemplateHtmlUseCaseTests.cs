using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;

public sealed class SaveTemplateHtmlUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly ISchemaInferenceService _schemaInference = Substitute.For<ISchemaInferenceService>();

    public SaveTemplateHtmlUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private SaveTemplateHtmlUseCase CreateSut() => new(
        _templateRepo,
        _storageService,
        _unitOfWork,
        _executionContext,
        _schemaInference);

    [Fact]
    public async Task SaveHtmlVersionAsync_ShouldUploadArchivedAndActiveKeys_AndIncrementVersion_AndPersistSchema()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var currentVersionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(projectId)
            .WithSlug("official-contract")
            .WithName("official-contract");
        template.SetCurrentVersion(currentVersionId, TestConstants.BaselineTime);

        TemplateVersion currentVersion = TemplateVersionBuilder.AVersion()
            .WithId(currentVersionId)
            .WithTemplateId(templateId)
            .WithVersion(3)
            .WithStorageKey("")
            .WithFormat(TemplateFormat.Html)
            .WithCreatedBy(null);

        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(currentVersionId, Arg.Any<CancellationToken>())
            .Returns(currentVersion);
        _executionContext.CallerApp.Returns("studio-user");

        string dummySchema = "{\"$schema\":\"http://json-schema.org/draft-07/schema#\",\"type\":\"object\"}";
        string dummySample = "{\"customerName\":\"คุณสมชาย\"}";

        _schemaInference.InferFromHtml(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((dummySchema, dummySample));

        var request = new SaveTemplateHtmlCommand(
            Html: "<html><body><h1>Contract Version 4</h1></body></html>",
            SamplePayload: dummySample,
            ChangeNote: "Updated payment terms"
        );

        // Act
        int nextVersion = await CreateSut().SaveHtmlVersionAsync(templateId, request);

        // Assert
        nextVersion.Should().Be(4);

        // Verify versioned archive upload
        await _storageService.Received(1).UploadAsync(
            "templates",
            $"templates/{projectId}/archive/official-contract_v4.html",
            Arg.Any<Stream>(),
            "text/html; charset=utf-8",
            Arg.Any<CancellationToken>());

        // Verify active upload
        await _storageService.Received(1).UploadAsync(
            "templates",
            $"templates/{projectId}/official-contract.html",
            Arg.Any<Stream>(),
            "text/html; charset=utf-8",
            Arg.Any<CancellationToken>());

        // Verify template updated with new version containing DataSchema and SamplePayload
        _templateRepo.Received(1).Update(
            Arg.Is<Template>(t => t.Versions.Any(v => v.Version == 4 &&
                                                      v.CommitMessage == "Updated payment terms" &&
                                                      v.DataSchema == dummySchema &&
                                                      v.SamplePayload == dummySample)));

        // Verify UoW saved
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
