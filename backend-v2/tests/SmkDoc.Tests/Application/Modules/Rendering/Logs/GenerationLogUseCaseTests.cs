using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs;

public class GenerationLogUseCaseTests
{
    private readonly Mock<IRepository<GenerationLog>> _mockRepo = new();
    private readonly Mock<IGenerationLogMetricsRepository> _mockMetricsRepo = new();

    [Fact]
    public async Task ListGenerationLogs_ShouldReturnPagedResult()
    {
        // Arrange
        var logs = new List<GenerationLog>
        {
            GenerationLogTestFactory.Create(
                templateId: Guid.NewGuid(),
                templateVersionId: Guid.NewGuid(),
                apiKeyId: Guid.NewGuid(),
                callerApp: "crm",
                triggerSource: "api",
                inputData: "req-1",
                outputKey: "out-1.pdf",
                outputFormat: OutputFormat.Pdf,
                fileSizeBytes: 1024,
                pageCount: 1,
                durationMs: 150,
                status: GenerationStatus.Success)
        };

        _mockRepo.Setup(r => r.PagedListAsync(
            It.IsAny<Expression<Func<GenerationLog, bool>>?>(),
            It.IsAny<Expression<Func<GenerationLog, DateTimeOffset>>>(),
            true,
            1,
            50,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 1));

        var useCase = new ListGenerationLogsUseCase(_mockRepo.Object);

        // Act
        var result = await useCase.ExecuteAsync(new ListGenerationLogsQuery(1, 50));

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Logs.First().CallerApp.Should().Be("crm");
    }

    [Fact]
    public async Task GetLogMetrics_ShouldReturnMetrics()
    {
        // Arrange
        var expected = new LogMetricsDto(100, 95, 5, 120.5, 50000, new List<AppUsageMetricDto>());
        _mockMetricsRepo.Setup(r => r.GetMetricsAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var useCase = new GetLogMetricsUseCase(_mockMetricsRepo.Object);

        // Act
        var result = await useCase.ExecuteAsync(new GetLogMetricsQuery());

        // Assert
        result.Should().NotBeNull();
        result.TotalGenerations.Should().Be(100);
        result.SuccessfulGenerations.Should().Be(95);
        result.AverageDurationMs.Should().Be(120.5);
    }
}
