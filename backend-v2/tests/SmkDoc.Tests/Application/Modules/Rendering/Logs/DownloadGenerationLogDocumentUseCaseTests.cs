using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs;

public sealed class DownloadGenerationLogDocumentUseCaseTests
{
    private readonly IGenerationLogRepository _logRepo = Substitute.For<IGenerationLogRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly Guid _projectId = Guid.NewGuid();

    public DownloadGenerationLogDocumentUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(_projectId));
        _projectContext.SystemRole.Returns((SystemRole?)null);
    }

    private DownloadGenerationLogDocumentUseCase CreateSut() =>
        new(_logRepo, _storageService, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextNotResolvedAndNotSuperAdmin_ShouldThrowTenantContextRequiredException()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithProjectId(new ProjectId(Guid.NewGuid()))
            .WithOutputKey("outputs/sample.pdf")
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);
        _projectContext.IsResolved.Returns(false);
        _projectContext.ProjectId.Returns(_ => throw new TenantContextRequiredException());
        var sut = CreateSut();
        var query = new DownloadGenerationLogDocumentQuery(logId);

        // Act
        var act = () => sut.ExecuteAsync(query);

        // Assert
        await act.Should().ThrowAsync<TenantContextRequiredException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var logId = Guid.NewGuid();
        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns((GenerationLog?)null);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(new DownloadGenerationLogDocumentQuery(logId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{logId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogBelongsToDifferentProject_ShouldThrowCrossTenantAccessDeniedException()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();
        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithProjectId(new ProjectId(foreignProjectId))
            .WithOutputKey("outputs/sample.pdf")
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(new DownloadGenerationLogDocumentQuery(logId));

        // Assert
        await act.Should().ThrowAsync<CrossTenantAccessDeniedException>()
            .WithMessage($"*{foreignProjectId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogHasNoOutputKey_ShouldThrowNotFoundException()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithProjectId(new ProjectId(_projectId))
            .WithOutputKey(string.Empty)
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(new DownloadGenerationLogDocumentQuery(logId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*No output file is associated*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldDownloadStreamAndReturnResult()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithProjectId(new ProjectId(_projectId))
            .WithOutputKey("outputs/doc123.pdf")
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);

        using var memoryStream = new MemoryStream([1, 2, 3, 4]);
        _storageService.DownloadAsync(StorageBuckets.Outputs, "outputs/doc123.pdf", Arg.Any<CancellationToken>())
            .Returns(memoryStream);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new DownloadGenerationLogDocumentQuery(logId));

        // Assert
        result.Should().NotBeNull();
        result.Stream.Should().BeSameAs(memoryStream);
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be("doc123.pdf");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdmin_ShouldAllowDownloadEvenForDifferentProject()
    {
        // Arrange
        _projectContext.SystemRole.Returns(SystemRole.SuperAdmin);
        var logId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();
        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithProjectId(new ProjectId(foreignProjectId))
            .WithOutputKey("outputs/doc_foreign.pdf")
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);

        using var memoryStream = new MemoryStream([10, 20]);
        _storageService.DownloadAsync(StorageBuckets.Outputs, "outputs/doc_foreign.pdf", Arg.Any<CancellationToken>())
            .Returns(memoryStream);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new DownloadGenerationLogDocumentQuery(logId));

        // Assert
        result.Should().NotBeNull();
        result.Stream.Should().BeSameAs(memoryStream);
    }
}

