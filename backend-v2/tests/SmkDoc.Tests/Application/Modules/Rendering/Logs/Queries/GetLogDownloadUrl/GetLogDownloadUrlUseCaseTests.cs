using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

public sealed class GetLogDownloadUrlUseCaseTests
{
    private readonly IRepository<GenerationLog> _logRepo = Substitute.For<IRepository<GenerationLog>>();
    private readonly IStorageService _storage = Substitute.For<IStorageService>();

    private GetLogDownloadUrlUseCase CreateSut() =>
        new(_logRepo, _storage);

    [Fact]
    public async Task ExecuteAsync_WhenLogExists_ReturnsPresignedUrl()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogBuilder.Success(id: logId, outputKey: "outputs/doc.pdf", durationMs: 1);

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);

        _storage.GetPresignedUrlAsync("outputs", "outputs/doc.pdf", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://minio.sammakorn.co.th/outputs/doc.pdf?token=abc");

        // Act
        var url = await CreateSut().ExecuteAsync(new GetLogDownloadUrlQuery(logId));

        // Assert
        url.Should().Contain("minio.sammakorn.co.th");
    }
}
