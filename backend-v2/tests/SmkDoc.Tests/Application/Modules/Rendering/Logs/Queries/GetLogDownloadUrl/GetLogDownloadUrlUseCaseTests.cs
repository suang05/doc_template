using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

public sealed class GetLogDownloadUrlUseCaseTests
{
    private readonly IGenerationLogRepository _logRepo = Substitute.For<IGenerationLogRepository>();

    private GetLogDownloadUrlUseCase CreateSut() =>
        new(_logRepo);

    [Fact]
    public async Task ExecuteAsync_WhenLogExists_ReturnsGatewayArtifactUrl()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogBuilder.Success(id: logId, outputKey: "outputs/doc.pdf", durationMs: 1);

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetLogDownloadUrlQuery(logId), CancellationToken.None);

        // Assert
        result.Url.Should().Be($"/api/v1/generation-logs/{logId:D}/artifact/file");
        result.ExpiresInSeconds.Should().Be(3600);
    }
}
