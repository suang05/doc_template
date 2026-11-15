using System.Text.Json;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands;

public sealed class EnqueueDocumentGenerationUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IDocumentJobRepository _documentJobRepo = Substitute.For<IDocumentJobRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public EnqueueDocumentGenerationUseCaseTests()
    {
        var projectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(projectId));
        _projectContext.CallerIdentity.Returns("erp-integration");
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private EnqueueDocumentGenerationUseCase CreateSut() =>
        new(_templateRepo, _documentJobRepo, _unitOfWork, _projectContext, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextNotResolved_ShouldThrowTenantContextNotResolvedException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.GetRequiredProjectId().Returns(_ => throw new TenantContextNotResolvedException());
        var sut = CreateSut();
        var command = new EnqueueDocumentGenerationCommand("invoice", JsonDocument.Parse("{}").RootElement);

        // Act
        var act = () => sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
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
        var act = () => sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*unknown-slug*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateValid_ShouldPersistDocumentJobAndReturnDto()
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
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Slug.Should().Be("receipt");
        result.Status.Should().Be(DocumentJobStatus.Queued.Name);
        result.CheckStatusUrl.Should().Be($"/api/v1/documents/jobs/{result.JobId}");

        await _documentJobRepo.Received(1).AddAsync(
            Arg.Is<DocumentJob>(j =>
                j.TemplateSlug == "receipt" &&
                j.Status == DocumentJobStatus.Queued &&
                j.CallerIdentity == "erp-integration" &&
                j.Items.Count == 1 &&
                j.Items.First().DocumentRef == "REC-2026-001"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
