using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.BackgroundWorkers;
using SmkDoc.Infrastructure.Services.Logging;
using SmkDoc.Tests.Common;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.BackgroundWorkers;

public sealed class GenerationLogBatchWorkerTests
{
    private readonly IGenerationLogRepository _logRepo = Substitute.For<IGenerationLogRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IServiceScope _scope = Substitute.For<IServiceScope>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public GenerationLogBatchWorkerTests()
    {
        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_serviceProvider);
        _serviceProvider.GetService(typeof(IGenerationLogRepository)).Returns(_logRepo);
        _serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private static GenerationLog CreateSampleLog()
    {
        var target = GenerationTarget.ForTemplate(
            TemplateId.New(),
            Guid.NewGuid(),
            OutputFormat.Pdf,
            "{}",
            new Sha256Hash("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));

        var context = new GenerationContext("test-client", "api", null);

        return GenerationLog.CreateSuccess(
            Guid.NewGuid(),
            ProjectId.New(),
            target,
            context,
            GenerationMetrics.Of(120, 2048),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBatchSizeReached_FlushesBatchToRepository()
    {
        // Arrange
        var queue = new ChannelGenerationLogQueue(capacity: 100);
        var options = Options.Create(new GenerationLogWorkerOptions
        {
            BatchSize = 3,
            FlushInterval = TimeSpan.FromSeconds(5)
        });

        var sut = new GenerationLogBatchWorker(
            queue,
            _scopeFactory,
            NullLogger<GenerationLogBatchWorker>.Instance,
            _timeProvider,
            options);

        await queue.EnqueueAsync(CreateSampleLog());
        await queue.EnqueueAsync(CreateSampleLog());
        await queue.EnqueueAsync(CreateSampleLog());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(200, CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        await _logRepo.Received().AddRangeAsync(
            Arg.Is<IEnumerable<GenerationLog>>(logs => logs.Count() == 3),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFlushIntervalExpires_FlushesPartialBatch()
    {
        // Arrange
        var queue = new ChannelGenerationLogQueue(capacity: 100);
        var options = Options.Create(new GenerationLogWorkerOptions
        {
            BatchSize = 10,
            FlushInterval = TimeSpan.FromMilliseconds(50)
        });

        var sut = new GenerationLogBatchWorker(
            queue,
            _scopeFactory,
            NullLogger<GenerationLogBatchWorker>.Instance,
            _timeProvider,
            options);

        // Enqueue only 1 item (less than BatchSize of 10)
        await queue.EnqueueAsync(CreateSampleLog());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(150, CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        // Assert: partial batch flushed due to flush interval timer
        await _logRepo.Received().AddRangeAsync(
            Arg.Is<IEnumerable<GenerationLog>>(logs => logs.Count() == 1),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHostShutdownRequested_DrainsRemainingLogs()
    {
        // Arrange
        var queue = new ChannelGenerationLogQueue(capacity: 100);
        var options = Options.Create(new GenerationLogWorkerOptions
        {
            BatchSize = 100,
            FlushInterval = TimeSpan.FromMinutes(1)
        });

        var sut = new GenerationLogBatchWorker(
            queue,
            _scopeFactory,
            NullLogger<GenerationLogBatchWorker>.Instance,
            _timeProvider,
            options);

        await queue.EnqueueAsync(CreateSampleLog());
        await queue.EnqueueAsync(CreateSampleLog());

        using var cts = new CancellationTokenSource();

        // Act
        await sut.StartAsync(cts.Token);
        // Trigger cancellation immediately
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        // Assert: remaining items drained during shutdown
        await _logRepo.Received().AddRangeAsync(
            Arg.Is<IEnumerable<GenerationLog>>(logs => logs.Count() == 2),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received().CommitAsync(Arg.Any<CancellationToken>());
    }
}
