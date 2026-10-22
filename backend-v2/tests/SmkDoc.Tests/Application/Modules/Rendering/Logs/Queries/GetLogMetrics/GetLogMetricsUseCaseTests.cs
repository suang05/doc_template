using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;

public sealed class GetLogMetricsUseCaseTests
{
    private readonly IGenerationLogMetricsRepository _metricsRepo = Substitute.For<IGenerationLogMetricsRepository>();

    private GetLogMetricsUseCase CreateSut() =>
        new(_metricsRepo);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsAggregatedMetrics()
    {
        // Arrange
        var expected = new LogMetricsDto(100, 95, 5, 120.5, 50000, new List<AppUsageMetricDto>());
        _metricsRepo.GetMetricsAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetLogMetricsQuery());

        // Assert
        result.Should().NotBeNull();
        result.TotalGenerations.Should().Be(100);
        result.SuccessfulGenerations.Should().Be(95);
        result.AverageDurationMs.Should().Be(120.5);
    }
}
