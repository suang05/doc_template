using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public sealed class ListGenerationLogsUseCaseTests
{
    private readonly IGenerationLogRepository _repo = Substitute.For<IGenerationLogRepository>();

    private ListGenerationLogsUseCase CreateSut() =>
        new(_repo);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsPagedResult()
    {
        // Arrange
        var logs = new List<GenerationLog>
        {
            new GenerationLogBuilder()
                .WithApiKeyId(Guid.NewGuid())
                .WithCallerApp("crm")
                .WithTriggerSource("api")
                .WithInputData("req-1")
                .WithOutputKey("out-1.pdf")
                .WithOutputFormat(OutputFormat.Pdf)
                .WithFileSizeBytes(1024)
                .WithPageCount(1)
                .WithDurationMs(150)
                .Build()
        };

        _repo.ListPagedAsync(
            "crm",
            1,
            50,
            Arg.Any<CancellationToken>())
            .Returns((logs, 1));

        // Act
        var result = await CreateSut().ExecuteAsync(new ListGenerationLogsQuery(1, 50, "crm"));

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Logs.First().CallerApp.Should().Be("crm");
    }
}
