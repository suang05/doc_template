using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Cache;
using StackExchange.Redis;

namespace SmkDoc.Tests.Infrastructure.Cache;

public sealed class HybridTemplateDraftCacheTests : IDisposable
{
    private readonly IConnectionMultiplexer _multiplexer = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly ILogger<HybridTemplateDraftCache> _logger = Substitute.For<ILogger<HybridTemplateDraftCache>>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ProjectId _projectId = ProjectId.New();

    public HybridTemplateDraftCacheTests()
    {
        _multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _multiplexer.Dispose();
    }

    private HybridTemplateDraftCache CreateSut(bool withRedis = true) =>
        new(withRedis ? _multiplexer : null, _storageService, _logger, _memoryCache);

    [Fact]
    public async Task GetAsync_WhenPresentInL1MemoryCache_ReturnsImmediatelyWithoutHittingRedis()
    {
        // Arrange
        var sut = CreateSut();
        var draftId = "draft-123";
        var entry = new TemplateDraftEntry(
            draftId,
            _projectId.Value,
            "drafts/test.html",
            "test.html",
            ".html",
            ["Name", "Date"]);

        // Pre-populate L1 memory cache
        var cacheKey = $"tmpl_draft:l1:{_projectId.Value:N}:{draftId}";
        _memoryCache.Set(cacheKey, entry);

        // Act
        var result = await sut.GetAsync(_projectId, draftId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DraftId.Should().Be(draftId);
        result.FileName.Should().Be("test.html");

        // Verify Redis was NEVER called because L1 memory cache hit
        await _database.DidNotReceive().StringGetAsync(Arg.Any<RedisKey>(), CommandFlags.None);
    }

    [Fact]
    public async Task GetAsync_WhenL1Miss_FetchesFromRedisAndBackfillsL1Cache()
    {
        // Arrange
        var sut = CreateSut();
        var draftId = "draft-456";
        var entry = new TemplateDraftEntry(
            draftId,
            _projectId.Value,
            "drafts/doc.docx",
            "doc.docx",
            ".docx",
            ["InvoiceId"]);

        var json = JsonSerializer.Serialize(entry, WebJsonOptions);
        var redisKey = $"smkdoc:drafts:{_projectId.Value:N}:{draftId}";

        _database.StringGetAsync(redisKey, CommandFlags.None)
            .Returns(new RedisValue(json));

        // Act 1: Initial call (L1 miss -> hits Redis)
        var result1 = await sut.GetAsync(_projectId, draftId, CancellationToken.None);

        // Assert 1
        result1.Should().NotBeNull();
        result1!.DraftId.Should().Be(draftId);
        await _database.Received(1).StringGetAsync(redisKey, CommandFlags.None);

        // Act 2: Second call (now cached in L1)
        var result2 = await sut.GetAsync(_projectId, draftId, CancellationToken.None);

        // Assert 2: L1 hit, Redis call count remains 1
        result2.Should().NotBeNull();
        await _database.Received(1).StringGetAsync(redisKey, CommandFlags.None);
    }

    [Fact]
    public async Task StoreAndGet_InStandaloneModeWithoutRedis_WorksPurelyOnL1()
    {
        // Arrange
        var sut = CreateSut(withRedis: false);
        using var stream = new MemoryStream("<h1>Hello Draft</h1>"u8.ToArray());
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("drafts/test.html");

        // Act
        var draftId = await sut.StoreAsync(_projectId, stream, "test.html", ".html", ["Name"], CancellationToken.None);
        var retrieved = await sut.GetAsync(_projectId, draftId, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.DraftId.Should().Be(draftId);
        retrieved.FileName.Should().Be("test.html");
    }

    [Fact]
    public async Task RemoveAsync_EvictsFromL1MemoryCacheAndDeletesFromRedisAndStorage()
    {
        // Arrange
        var sut = CreateSut();
        var draftId = "draft-789";
        var entry = new TemplateDraftEntry(
            draftId,
            _projectId.Value,
            "drafts/remove.html",
            "remove.html",
            ".html",
            []);

        var l1Key = $"tmpl_draft:l1:{_projectId.Value:N}:{draftId}";
        _memoryCache.Set(l1Key, entry);

        var json = JsonSerializer.Serialize(entry, WebJsonOptions);
        var redisKey = $"smkdoc:drafts:{_projectId.Value:N}:{draftId}";
        _database.StringGetDeleteAsync(redisKey, CommandFlags.None).Returns(new RedisValue(json));

        // Act
        await sut.RemoveAsync(_projectId, draftId, CancellationToken.None);

        // Assert
        _memoryCache.TryGetValue(l1Key, out _).Should().BeFalse();
        await _database.Received(1).StringGetDeleteAsync(redisKey, CommandFlags.None);
        await _storageService.Received(1).DeleteAsync(Arg.Any<string>(), entry.StorageKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentProjectDraftAsync_WhenContextResolved_ReturnsCachedDraft()
    {
        // Arrange
        var projectContext = Substitute.For<ICurrentProjectContext>();
        projectContext.TryGetProjectId().Returns(_projectId);
        var sut = new HybridTemplateDraftCache(null, _storageService, _logger, _memoryCache, "smkdoc:", projectContext);

        var draftId = "draft-current";
        var entry = new TemplateDraftEntry(draftId, _projectId.Value, "drafts/c.html", "c.html", ".html", ["Name"]);
        _memoryCache.Set($"tmpl_draft:l1:{_projectId.Value:N}:{draftId}", entry);

        // Act
        var result = await sut.GetCurrentProjectDraftAsync(draftId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DraftId.Should().Be(draftId);
    }

    [Fact]
    public async Task GetCurrentProjectDraftAsync_WhenContextMissing_ThrowsProjectContextRequiredException()
    {
        // Arrange
        var projectContext = Substitute.For<ICurrentProjectContext>();
        projectContext.TryGetProjectId().Returns((ProjectId?)null);
        var sut = new HybridTemplateDraftCache(null, _storageService, _logger, _memoryCache, "smkdoc:", projectContext);

        // Act
        var act = () => sut.GetCurrentProjectDraftAsync("draft-missing-ctx", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ProjectContextRequiredException>();
    }
}
