using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

public class GetLogDownloadUrlUseCaseTests
{
    private readonly Mock<IRepository<GenerationLog>> _logRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();

    private GetLogDownloadUrlUseCase CreateSut() =>
        new(_logRepoMock.Object, _storageMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenLogExists_ReturnsPresignedUrl()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogTestFactory.Create(id: logId, outputKey: "outputs/doc.pdf", durationMs: 1);

        _logRepoMock
            .Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        _storageMock
            .Setup(s => s.GetPresignedUrlAsync("outputs", "outputs/doc.pdf", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.sammakorn.co.th/outputs/doc.pdf?token=abc");

        // Act
        var url = await CreateSut().ExecuteAsync(new GetLogDownloadUrlQuery(logId));

        // Assert
        url.Should().Contain("minio.sammakorn.co.th");
    }
}
