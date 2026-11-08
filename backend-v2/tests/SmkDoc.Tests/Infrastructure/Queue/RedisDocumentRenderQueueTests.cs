using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Models;
using SmkDoc.Infrastructure.Queue;
using StackExchange.Redis;

namespace SmkDoc.Tests.Infrastructure.Queue;

public sealed class RedisDocumentRenderQueueTests
{
    private readonly IConnectionMultiplexer _multiplexer = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly ILogger<RedisDocumentRenderQueue> _logger = Substitute.For<ILogger<RedisDocumentRenderQueue>>();
    private readonly RedisDocumentRenderQueue _sut;

    public RedisDocumentRenderQueueTests()
    {
        _multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _sut = new RedisDocumentRenderQueue(_multiplexer, _logger, "test:");
    }

    [Fact]
    public async Task EnqueueAsync_ShouldPersistJobAndAddToStream()
    {
        // Arrange
        var job = new DocumentJob(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "caller",
            "invoice",
            "{\"amount\":100}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        // Act
        var resultId = await _sut.EnqueueAsync(job);

        // Assert
        resultId.Should().Be(job.JobId);
        await _database.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(job.JobId.ToString())),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            false,
            When.Always,
            CommandFlags.None);

        await _database.Received(1).StreamAddAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "test:render:stream"),
            Arg.Is<NameValueEntry[]>(entries => entries.Length == 1 && entries[0].Name == "jobId" && entries[0].Value == job.JobId.ToString()),
            Arg.Any<RedisValue?>(),
            Arg.Any<int?>(),
            false,
            CommandFlags.None);
    }

    [Fact]
    public async Task DequeueAsync_WhenStreamHasEntry_ShouldReturnJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            Guid.NewGuid(),
            "caller",
            "invoice",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(job, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        _database.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains(jobId.ToString())), CommandFlags.None)
            .Returns(new RedisValue(json));

        var streamEntries = new[]
        {
            new StreamEntry(new RedisValue("100-0"), [new NameValueEntry("jobId", jobId.ToString())])
        };

        _database.StreamReadGroupAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "test:render:stream"),
            Arg.Is<RedisValue>(v => v.ToString() == "test:render:group"),
            Arg.Any<RedisValue>(),
            Arg.Any<RedisValue?>(),
            Arg.Any<int?>(),
            Arg.Any<bool>(),
            Arg.Any<CommandFlags>())
            .Returns(streamEntries);

        // Act
        var result = await _sut.DequeueAsync();

        // Assert
        result.Should().NotBeNull();
        result!.JobId.Should().Be(jobId);
    }

    [Fact]
    public async Task UpdateJobAsync_WhenCompleted_ShouldAcknowledgeStreamMessage()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new DocumentJob(
            jobId,
            Guid.NewGuid(),
            "caller",
            "invoice",
            "{}",
            "pdf",
            null,
            null,
            false,
            DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(job, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        _database.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains(jobId.ToString())), CommandFlags.None)
            .Returns(new RedisValue(json));

        var streamEntries = new[]
        {
            new StreamEntry(new RedisValue("200-0"), [new NameValueEntry("jobId", jobId.ToString())])
        };

        _database.StreamReadGroupAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "test:render:stream"),
            Arg.Is<RedisValue>(v => v.ToString() == "test:render:group"),
            Arg.Any<RedisValue>(),
            Arg.Any<RedisValue?>(),
            Arg.Any<int?>(),
            Arg.Any<bool>(),
            Arg.Any<CommandFlags>())
            .Returns(streamEntries);

        // Dequeue first to track active stream id
        var dequeued = await _sut.DequeueAsync();
        dequeued.Should().NotBeNull();

        // Mark completed
        dequeued!.MarkCompleted(new GenerateDocumentResultDto(
            Guid.NewGuid(), "INV", "pdf", 100, "hash", "url", DateTimeOffset.UtcNow), DateTimeOffset.UtcNow);

        // Act
        await _sut.UpdateJobAsync(dequeued);

        // Assert
        await _database.Received(1).StreamAcknowledgeAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "test:render:stream"),
            Arg.Is<RedisValue>(v => v.ToString() == "test:render:group"),
            Arg.Is<RedisValue>(v => v.ToString() == "200-0"),
            CommandFlags.None);

        await _database.Received(1).StreamDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "test:render:stream"),
            Arg.Is<RedisValue[]>(ids => ids.Length == 1 && ids[0].ToString() == "200-0"),
            CommandFlags.None);
    }
}

