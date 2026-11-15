using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries;

public sealed class GetDocumentJobUseCaseTests
{
    private readonly IDocumentJobReadModel _readModel = Substitute.For<IDocumentJobReadModel>();

    private GetDocumentJobUseCase CreateSut() => new(_readModel);

    [Fact]
    public async Task ExecuteAsync_WhenJobNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _readModel.FindByIdAsync(new DocumentJobId(jobId), Arg.Any<CancellationToken>())
            .Returns((DocumentJobResultDto?)null);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(new GetDocumentJobQuery(jobId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{jobId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobFound_ShouldReturnProjectedLegacyDto()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var dto = new DocumentJobResultDto(
            JobId: jobId,
            TemplateSlug: "receipt",
            TemplateId: templateId,
            TemplateVersionId: versionId,
            Status: DocumentJobStatus.Succeeded.Name,
            OutputFormat: "pdf",
            Attempt: 1,
            MaxAttempts: 3,
            CancelRequested: false,
            TotalItems: 1,
            SucceededItems: 1,
            FailedItems: 0,
            ResultGenerationLogId: null,
            DownloadUrl: $"/api/v1/document-jobs/{jobId}/result",
            ErrorCode: null,
            ErrorMessage: null,
            CreatedAt: now,
            StartedAt: now,
            CompletedAt: now,
            Version: 2);

        _readModel.FindByIdAsync(new DocumentJobId(jobId), Arg.Any<CancellationToken>())
            .Returns(dto);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDocumentJobQuery(jobId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().Be(jobId);
        result.Slug.Should().Be("receipt");
        result.Status.Should().Be(DocumentJobStatus.Succeeded.Name);
        result.CheckStatusUrl.Should().Be($"/api/v1/documents/jobs/{jobId}");
        result.DownloadUrl.Should().Be($"/api/v1/document-jobs/{jobId}/result");
    }
}
