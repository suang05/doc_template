using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Infrastructure.Queue;

namespace SmkDoc.Tests.Infrastructure.Queue;

public sealed class MemoryDocumentRenderQueueTests
{
    private readonly MemoryDocumentRenderQueue _queue = new();

    [Fact]
    public async Task EnqueueAndDequeue_ShouldProcessInOrder()
    {
        // Arrange
        var job1 = new DocumentJob(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            "caller1",
            "slug1",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        var job2 = new DocumentJob(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            "caller2",
            "slug2",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        // Act
        var id1 = await _queue.EnqueueAsync(job1, CancellationToken.None);
        var id2 = await _queue.EnqueueAsync(job2, CancellationToken.None);

        id1.Should().Be(job1.JobId);
        id2.Should().Be(job2.JobId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var dequeued1 = await _queue.DequeueAsync(cts.Token);
        var dequeued2 = await _queue.DequeueAsync(cts.Token);

        // Assert
        dequeued1.Should().NotBeNull();
        dequeued1!.JobId.Should().Be(job1.JobId);

        dequeued2.Should().NotBeNull();
        dequeued2!.JobId.Should().Be(job2.JobId);
    }

    [Fact]
    public async Task GetJobAsync_WhenJobExists_ShouldReturnJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            Guid.NewGuid(),
            "caller",
            "slug",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        await _queue.EnqueueAsync(job, CancellationToken.None);

        // Act
        var retrieved = await _queue.GetJobAsync(jobId, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.JobId.Should().Be(jobId);
    }

    [Fact]
    public async Task GetJobAsync_WhenJobDoesNotExist_ShouldReturnNull()
    {
        // Act
        var retrieved = await _queue.GetJobAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_ShouldPersistModifications()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            Guid.NewGuid(),
            "caller",
            "slug",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        await _queue.EnqueueAsync(job, CancellationToken.None);

        // Act
        job.MarkProcessing(DateTimeOffset.UtcNow);
        await _queue.UpdateJobAsync(job, CancellationToken.None);

        var updated = await _queue.GetJobAsync(jobId, CancellationToken.None);

        // Assert
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(DocumentJobStatus.Processing);
        updated.StartedAt.Should().NotBeNull();
    }
}

