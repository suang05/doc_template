using System.Text.Json;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands;

public sealed class EnqueueDocumentGenerationUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IDocumentRenderQueue _renderQueue = Substitute.For<IDocumentRenderQueue>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public EnqueueDocumentGenerationUseCaseTests()
    {
        var projectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));
        _projectContext.CallerIdentity.Returns("erp-integration");
    }

    private EnqueueDocumentGenerationUseCase CreateSut()
    {
        return new EnqueueDocumentGenerationUseCase(
            _templateRepo,
            _renderQueue,
            _projectContext,
            _timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextNotResolved_ShouldThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var sut = CreateSut();
        var command = new EnqueueDocumentGenerationCommand("invoice", JsonDocument.Parse("{}").RootElement);

        // Act
        var act = () => sut.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Active project context is required*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _templateRepo.GetBySlugWithDetailsAsync("unknown-slug", Arg.Any<CancellationToken>())
            .Returns((Template?)null);
        var sut = CreateSut();
        var command = new EnqueueDocumentGenerationCommand("unknown-slug", JsonDocument.Parse("{}").RootElement);

        // Act
        var act = () => sut.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*unknown-slug*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateValid_ShouldEnqueueJobAndReturnDto()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithSlug("receipt")
            .WithCurrentVersion(versionId)
            .Build();

        var version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .Build();

        template.PublishVersion(version, DateTimeOffset.UtcNow);

        _templateRepo.GetBySlugWithDetailsAsync("receipt", Arg.Any<CancellationToken>())
            .Returns(template);

        var sut = CreateSut();
        var command = new EnqueueDocumentGenerationCommand(
            "receipt",
            JsonDocument.Parse("{\"amount\": 500}").RootElement,
            OutputFormat: "pdf",
            DocumentRef: "REC-2026-001");

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Slug.Should().Be("receipt");
        result.Status.Should().Be(nameof(DocumentJobStatus.Queued));
        result.CheckStatusUrl.Should().Be($"/api/v1/documents/jobs/{result.JobId}");

        await _renderQueue.Received(1).EnqueueAsync(
            Arg.Is<DocumentJob>(j =>
                j.Slug == "receipt" &&
                j.Status == DocumentJobStatus.Queued &&
                j.DocumentRef == "REC-2026-001" &&
                j.CallerIdentity == "erp-integration"),
            Arg.Any<CancellationToken>());
    }
}
