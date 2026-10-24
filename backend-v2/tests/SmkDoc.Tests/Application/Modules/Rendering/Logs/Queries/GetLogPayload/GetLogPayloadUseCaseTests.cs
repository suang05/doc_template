using System.Text;
using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogPayload;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Logs.Queries.GetLogPayload;

public sealed class GetLogPayloadUseCaseTests
{
    private readonly IRepository<GenerationLog> _logRepo = Substitute.For<IRepository<GenerationLog>>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();

    private GetLogPayloadUseCase CreateSut() => new(_logRepo, _storageService);

    [Fact]
    public async Task ExecuteAsync_WhenLogDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var logId = Guid.NewGuid();
        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>()).Returns((GenerationLog?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetLogPayloadQuery(logId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{logId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogHasInlinePayload_ReturnsPayloadDirectly()
    {
        // Arrange
        var logId = Guid.NewGuid();
        const string expectedJson = "{\"name\":\"Somchai\",\"amount\":1500}";
        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithInputData(expectedJson)
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>()).Returns(log);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetLogPayloadQuery(logId));

        // Assert
        result.LogId.Should().Be(logId);
        result.PayloadJson.Should().Be(expectedJson);
        result.IsOffloaded.Should().BeFalse();
        result.OffloadedKey.Should().BeNull();
        await _storageService.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogHasOffloadedPayload_DownloadsFromMinIoAndDeReferences()
    {
        // Arrange
        var logId = Guid.NewGuid();
        const string objectKey = "audit/proj-123/2026/10/24/abc123hash.json";
        var inputData = $"[OFFLOADED:{objectKey}]";
        const string offloadedContent = "{\"largeArray\":[1,2,3,4,5],\"status\":\"Approved\"}";

        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithInputData(inputData)
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>()).Returns(log);

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(offloadedContent));
        _storageService.DownloadAsync(StorageBuckets.AuditPayloads, objectKey, Arg.Any<CancellationToken>())
            .Returns(memoryStream);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetLogPayloadQuery(logId));

        // Assert
        result.LogId.Should().Be(logId);
        result.PayloadJson.Should().Be(offloadedContent);
        result.IsOffloaded.Should().BeTrue();
        result.OffloadedKey.Should().Be(objectKey);
        await _storageService.Received(1).DownloadAsync(StorageBuckets.AuditPayloads, objectKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogHasNullInputData_ReturnsNullPayload()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogBuilder.ALog()
            .WithId(logId)
            .WithInputData(null)
            .Build();

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>()).Returns(log);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetLogPayloadQuery(logId));

        // Assert
        result.LogId.Should().Be(logId);
        result.PayloadJson.Should().BeNull();
        result.IsOffloaded.Should().BeFalse();
        result.OffloadedKey.Should().BeNull();
    }
}
