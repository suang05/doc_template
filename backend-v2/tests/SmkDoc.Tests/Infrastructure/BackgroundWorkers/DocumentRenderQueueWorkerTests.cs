using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.BackgroundWorkers;

namespace SmkDoc.Tests.Infrastructure.BackgroundWorkers;

public sealed class DocumentRenderQueueWorkerTests
{
    private readonly IDocumentRenderQueue _queue = Substitute.For<IDocumentRenderQueue>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly ILogger<DocumentRenderQueueWorker> _logger = Substitute.For<ILogger<DocumentRenderQueueWorker>>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    [Fact]
    public async Task ExecuteAsync_WhenJobDequeued_ShouldExecuteGenerateUseCaseAndMarkCompleted()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            projectId,
            "m2m-caller",
            "invoice",
            "{\"amount\":100}",
            "pdf",
            "INV-001",
            null,
            false,
            _timeProvider.GetUtcNow());

        using var cts = new CancellationTokenSource();

        // Queue returns the job once, then cancels
        _queue.DequeueAsync(Arg.Any<CancellationToken>())
            .Returns(
                callInfo => Task.FromResult<DocumentJob?>(job),
                callInfo =>
                {
                    cts.Cancel();
                    return Task.FromResult<DocumentJob?>(null);
                });

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        _scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        var projectSetter = Substitute.For<ICurrentProjectContextSetter>();
        serviceProvider.GetService(typeof(ICurrentProjectContextSetter)).Returns(projectSetter);

        var generateUseCase = Substitute.For<IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>>();
        var expectedResult = new GenerateDocumentResultDto(
            Guid.NewGuid(),
            "INV-001",
            "pdf",
            2048,
            "sha256hash",
            "https://minio/outputs/inv.pdf",
            DateTimeOffset.UtcNow.AddHours(1));

        generateUseCase.ExecuteAsync(Arg.Any<GenerateDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        serviceProvider.GetService(typeof(IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>))
            .Returns(generateUseCase);

        var options = Options.Create(new DocumentRenderWorkerOptions
        {
            MaxConcurrentRenders = 2,
            PollDelay = TimeSpan.FromMilliseconds(50)
        });

        var worker = new DocumentRenderQueueWorker(
            _queue,
            _scopeFactory,
            _logger,
            _timeProvider,
            options);

        // Act
        await worker.StartAsync(cts.Token);
        // Allow background thread to process job
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        projectSetter.Received(1).SetProject(new ProjectId(projectId), "m2m-caller");
        await generateUseCase.Received(1).ExecuteAsync(
            Arg.Is<GenerateDocumentCommand>(c => c.Slug == "invoice" && c.OutputFormat == "pdf" && c.DocumentRef == "INV-001"),
            Arg.Any<CancellationToken>());

        job.Status.Should().Be(DocumentJobStatus.Completed);
        job.Result.Should().Be(expectedResult);
        await _queue.Received().UpdateJobAsync(job, Arg.Any<CancellationToken>());
    }
}

