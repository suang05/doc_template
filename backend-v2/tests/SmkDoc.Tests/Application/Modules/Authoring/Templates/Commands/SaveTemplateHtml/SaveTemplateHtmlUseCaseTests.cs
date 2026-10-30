using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
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
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ISchemaInferenceService _schemaInference = Substitute.For<ISchemaInferenceService>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public SaveTemplateHtmlUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(TestConstants.DefaultProjectId));
        _projectContext.CallerIdentity.Returns("studio-user");
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private SaveTemplateHtmlUseCase CreateSut() => new(
        _templateRepo,
        _storageService,
        _unitOfWork,
        _projectContext,
        _schemaInference,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_ShouldUploadOnlyImmutableVersionKey_ArchivePreviousVersion_AndCarryForwardMappingsSnapshot()
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
            .WithStorageKey($"templates/{projectId:N}/{templateId:N}/v3.html")
            .WithFormat(TemplateFormat.Html)
            .WithStatus("Published")
            .WithCreatedBy("author");
        currentVersion.UpdateMappingsSnapshot("[{\"placeholder\":\"customerName\"}]", TestConstants.BaselineTime);

        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(currentVersionId, Arg.Any<CancellationToken>())
            .Returns(currentVersion);

        string dummySchema = "{\"$schema\":\"http://json-schema.org/draft-07/schema#\",\"type\":\"object\"}";
        string dummySample = "{\"customerName\":\"คุณสมชาย\"}";

        _schemaInference.InferFromHtml(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns((dummySchema, dummySample));

        var request = new SaveTemplateHtmlCommand(
            TemplateId: templateId,
            Html: "<html><body><h1>Contract Version 4</h1></body></html>",
            SamplePayload: dummySample,
            ChangeNote: "Updated payment terms"
        );

        // Act
        int nextVersion = await CreateSut().ExecuteAsync(request);

        // Assert
        nextVersion.Should().Be(4);
        var expectedImmutableKey = $"templates/{projectId:N}/{templateId:N}/v4.html";

        // Verify ONLY ONE immutable upload (no mutable activeKey overwrite)
        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Templates,
            expectedImmutableKey,
            Arg.Any<Stream>(),
            "text/html; charset=utf-8",
            Arg.Any<CancellationToken>());
        await _storageService.Received(1).UploadAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Previous Published version was archived
        currentVersion.Status.Should().Be(TemplateVersionStatus.Archived);

        // Verify template updated with new version containing DataSchema, SamplePayload, and MappingsSnapshot
        _templateRepo.Received(1).Update(
            Arg.Is<Template>(t => t.Versions.Any(v =>
                v.Version == 4 &&
                v.StorageKey == expectedImmutableKey &&
                v.Status == TemplateVersionStatus.Published &&
                v.CommitMessage == "Updated payment terms" &&
                v.DataSchema == dummySchema &&
                v.SamplePayload == dummySample &&
                v.MappingsSnapshot == "[{\"placeholder\":\"customerName\"}]")));

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDbCommitFails_ShouldDeleteOrphanedVersionObjectFromMinio()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithProjectId(projectId)
            .WithSlug("official-contract")
            .WithName("official-contract");

        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _schemaInference.InferFromHtml(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(("{}", "{}"));
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB commit failed"));

        var request = new SaveTemplateHtmlCommand(templateId, "<html></html>", "{}", "Note");

        // Act
        var act = () => CreateSut().ExecuteAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB commit failed");
        await _storageService.Received(1).DeleteAsync(
            StorageBuckets.Templates,
            $"templates/{projectId:N}/{templateId:N}/v1.html",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var request = new SaveTemplateHtmlCommand(Guid.NewGuid(), "<html></html>", "{}", "Note");

        // Act
        var act = () => CreateSut().ExecuteAsync(request);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
