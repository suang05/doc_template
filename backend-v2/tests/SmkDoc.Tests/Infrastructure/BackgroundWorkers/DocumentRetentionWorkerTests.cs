using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Infrastructure.BackgroundWorkers;

namespace SmkDoc.Tests.Infrastructure.BackgroundWorkers;

public sealed class DocumentRetentionWorkerTests
{
    private readonly IDocumentRetentionProcessor _retentionProcessor = Substitute.For<IDocumentRetentionProcessor>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IServiceScope _scope = Substitute.For<IServiceScope>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public DocumentRetentionWorkerTests()
    {
        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_serviceProvider);
        _serviceProvider.GetService(typeof(IDocumentRetentionProcessor)).Returns(_retentionProcessor);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTriggered_CallsPurgeOnRetentionService()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<bool>();

        var options = Options.Create(new DocumentRetentionWorkerOptions
        {
            RetentionPeriod = TimeSpan.FromHours(24),
            CheckInterval = TimeSpan.FromMilliseconds(1),
            BatchSize = 50
        });

        _retentionProcessor.PurgeExpiredOutputsAsync(Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                tcs.TrySetResult(true);
                return Task.FromResult(0);
            });

        using var sut = new DocumentRetentionWorker(
            _scopeFactory,
            TimeProvider.System,
            NullLogger<DocumentRetentionWorker>.Instance,
            options);

        // Act
        await sut.StartAsync(cts.Token);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        await _retentionProcessor.Received().PurgeExpiredOutputsAsync(
            TimeSpan.FromHours(24),
            50,
            Arg.Any<CancellationToken>());
    }
}
