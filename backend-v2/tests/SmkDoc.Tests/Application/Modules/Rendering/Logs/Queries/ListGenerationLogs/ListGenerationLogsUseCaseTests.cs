using System.Linq.Expressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public class ListGenerationLogsUseCaseTests
{
    private readonly Mock<IRepository<GenerationLog>> _repoMock = new();

    private ListGenerationLogsUseCase CreateSut() =>
        new(_repoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsPagedResult()
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

        _repoMock.Setup(r => r.PagedListAsync(
            It.IsAny<Expression<Func<GenerationLog, bool>>?>(),
            It.IsAny<Expression<Func<GenerationLog, DateTimeOffset>>>(),
            true,
            1,
            50,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 1));

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new ListGenerationLogsQuery(1, 50));

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Logs.First().CallerApp.Should().Be("crm");
    }
}
