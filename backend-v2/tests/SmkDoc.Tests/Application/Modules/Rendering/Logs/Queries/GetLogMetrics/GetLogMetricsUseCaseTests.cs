using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;

public class GetLogMetricsUseCaseTests
{
    private readonly Mock<IGenerationLogMetricsRepository> _metricsRepoMock = new();

    private GetLogMetricsUseCase CreateSut() =>
        new(_metricsRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsAggregatedMetrics()
    {
        // Arrange
        var expected = new LogMetricsDto(100, 95, 5, 120.5, 50000, new List<AppUsageMetricDto>());
        _metricsRepoMock
            .Setup(r => r.GetMetricsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetLogMetricsQuery());

        // Assert
        result.Should().NotBeNull();
        result.TotalGenerations.Should().Be(100);
        result.SuccessfulGenerations.Should().Be(95);
        result.AverageDurationMs.Should().Be(120.5);
    }
}
