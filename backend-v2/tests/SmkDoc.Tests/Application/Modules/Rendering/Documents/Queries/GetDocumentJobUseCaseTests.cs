using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries;

public sealed class GetDocumentJobUseCaseTests
{
    private readonly IDocumentRenderQueue _renderQueue = Substitute.For<IDocumentRenderQueue>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly Guid _projectId = Guid.NewGuid();

    public GetDocumentJobUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(_projectId));
        _projectContext.SystemRole.Returns((SystemRole?)null);
    }

    private GetDocumentJobUseCase CreateSut() => new(_renderQueue, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextNotResolvedAndNotSuperAdmin_ShouldThrowTenantContextRequiredException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            Guid.NewGuid(),
            "caller",
            "invoice",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        _renderQueue.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(job);
        _projectContext.IsResolved.Returns(false);
        _projectContext.SystemRole.Returns((SystemRole?)null);
        _projectContext.ProjectId.Returns(_ => throw new TenantContextRequiredException());
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(new GetDocumentJobQuery(jobId));

        // Assert
        await act.Should().ThrowAsync<TenantContextRequiredException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _renderQueue.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns((DocumentJob?)null);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(new GetDocumentJobQuery(jobId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{jobId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobBelongsToDifferentProject_ShouldThrowCrossTenantAccessDeniedException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            foreignProjectId,
            "caller",
            "invoice",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        _renderQueue.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(job);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(new GetDocumentJobQuery(jobId));

        // Assert
        await act.Should().ThrowAsync<CrossTenantAccessDeniedException>()
            .WithMessage($"*{foreignProjectId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobFoundInSameProject_ShouldReturnDto()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            _projectId,
            "caller",
            "receipt",
            "{}",
            "pdf",
            "REC-001",
            null,
            false,
            DateTimeOffset.UtcNow);

        _renderQueue.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(job);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDocumentJobQuery(jobId));

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().Be(jobId);
        result.Slug.Should().Be("receipt");
        result.Status.Should().Be(nameof(DocumentJobStatus.Queued));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdmin_ShouldReturnDtoEvenForDifferentProject()
    {
        // Arrange
        _projectContext.SystemRole.Returns(SystemRole.SuperAdmin);
        var jobId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            foreignProjectId,
            "caller",
            "receipt",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        _renderQueue.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(job);
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDocumentJobQuery(jobId));

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().Be(jobId);
    }
}

