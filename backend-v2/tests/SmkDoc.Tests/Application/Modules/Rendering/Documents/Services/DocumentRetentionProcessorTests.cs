using SmkDoc.Application.Common;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentRetentionProcessorTests
{
    private readonly IGenerationLogRepository _logRepo = Substitute.For<IGenerationLogRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private DocumentRetentionProcessor CreateSut()
    {
        return new DocumentRetentionProcessor(
            _logRepo,
            _storageService,
            _unitOfWork,
            _timeProvider);
    }

    private static GenerationLog CreateLogWithOutput(string outputKey, DateTimeOffset createdAt)
    {
        var target = GenerationTarget.ForTemplate(
            TemplateId.New(),
            Guid.NewGuid(),
            OutputFormat.Pdf,
            "{}",
            outputKey,
            new Sha256Hash("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));

        var context = new GenerationContext("test-client", "api", null);
        return GenerationLog.CreateSuccess(
            Guid.NewGuid(),
            ProjectId.New(),
            target,
            context,
            GenerationMetrics.Of(100, 1024),
            createdAt);
    }

    [Fact]
    public async Task PurgeExpiredOutputsAsync_WhenNoExpiredLogs_ShouldReturnZero()
    {
        // Arrange
        _logRepo.GetExpiredLogsWithOutputsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = CreateSut();

        // Act
        var result = await sut.PurgeExpiredOutputsAsync(TimeSpan.FromHours(24), 100);

        // Assert
        result.Should().Be(0);
        await _storageService.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeExpiredOutputsAsync_WhenExpiredLogsExist_ShouldDeleteFromStorageAndMarkPurged()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var expiredLog1 = CreateLogWithOutput("outputs/log1.pdf", now.AddHours(-25));
        var expiredLog2 = CreateLogWithOutput("outputs/log2.pdf", now.AddHours(-26));

        _logRepo.GetExpiredLogsWithOutputsAsync(Arg.Any<DateTimeOffset>(), 100, Arg.Any<CancellationToken>())
            .Returns([expiredLog1, expiredLog2]);

        var sut = CreateSut();

        // Act
        var result = await sut.PurgeExpiredOutputsAsync(TimeSpan.FromHours(24), 100);

        // Assert
        result.Should().Be(2);
        await _storageService.Received(1).DeleteAsync(StorageBuckets.Outputs, "outputs/log1.pdf", Arg.Any<CancellationToken>());
        await _storageService.Received(1).DeleteAsync(StorageBuckets.Outputs, "outputs/log2.pdf", Arg.Any<CancellationToken>());

        expiredLog1.OutputKey.Should().BeNull();
        expiredLog2.OutputKey.Should().BeNull();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeExpiredOutputsAsync_WhenStorageDeleteThrows_ShouldContinueAndMarkPurged()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var expiredLog = CreateLogWithOutput("outputs/failed-delete.pdf", now.AddHours(-25));

        _logRepo.GetExpiredLogsWithOutputsAsync(Arg.Any<DateTimeOffset>(), 100, Arg.Any<CancellationToken>())
            .Returns([expiredLog]);

        _storageService.DeleteAsync(StorageBuckets.Outputs, "outputs/failed-delete.pdf", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        var sut = CreateSut();

        // Act
        var result = await sut.PurgeExpiredOutputsAsync(TimeSpan.FromHours(24), 100);

        // Assert
        result.Should().Be(1);
        expiredLog.OutputKey.Should().BeNull();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
