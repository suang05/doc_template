using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

public sealed class ListGenerationLogsUseCaseTests
{
    private readonly IGenerationLogQueryService _logQueryService = Substitute.For<IGenerationLogQueryService>();

    private ListGenerationLogsUseCase CreateSut() =>
        new(_logQueryService);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_DelegatesToQueryServiceAndReturnsPagedResult()
    {
        // Arrange
        var dto = new GenerationLogDto(
            Id: Guid.NewGuid(),
            TemplateId: Guid.NewGuid(),
            TemplateVersionId: Guid.NewGuid(),
            ApiKeyId: Guid.NewGuid(),
            CallerApp: "crm",
            TriggerSource: "api",
            OutputFormat: "pdf",
            FileSizeBytes: 1024,
            PageCount: 1,
            DurationMs: 150,
            Status: GenerationStatus.Success,
            ErrorMsg: null,
            CreatedAt: DateTimeOffset.UnixEpoch);

        _logQueryService.ListPagedAsync(
                "crm",
                1,
                50,
                false,
                Arg.Any<CancellationToken>())
            .Returns(([dto], (long?)null));

        // Act
        var result = await CreateSut().ExecuteAsync(new ListGenerationLogsQuery(1, 50, "crm"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().ContainSingle().Which.CallerApp.Should().Be("crm");
        result.Total.Should().BeNull();
        result.Page.Should().Be(1);
        result.Limit.Should().Be(50);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIncludeTotalTrue_ReturnsTotalCount()
    {
        // Arrange
        _logQueryService.ListPagedAsync(
                "crm",
                2,
                25,
                true,
                Arg.Any<CancellationToken>())
            .Returns(([], (long?)75L));

        // Act
        var result = await CreateSut().ExecuteAsync(
            new ListGenerationLogsQuery(2, 25, "crm", IncludeTotal: true),
            CancellationToken.None);

        // Assert
        result.Total.Should().Be(75L);
        result.Page.Should().Be(2);
        result.Limit.Should().Be(25);
    }
}
