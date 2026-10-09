using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Infrastructure.Cache;
using Xunit;

namespace SmkDoc.Tests.Infrastructure.Cache;

public class MemoryIdempotencyStoreTests
{
    private readonly MemoryIdempotencyStore sut;

    public MemoryIdempotencyStoreTests()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        sut = new MemoryIdempotencyStore(memoryCache);
    }

    [Fact]
    public async Task TryAcquireOrGetAsync_WhenKeyIsNew_AcquiresLockSuccessfully()
    {
        var cacheKey = "idempotency:app1:key-1";
        var fingerprint = "fingerprint-abc";
        var ttl = TimeSpan.FromMinutes(2);

        var result = await sut.TryAcquireOrGetAsync(cacheKey, fingerprint, ttl);

        result.IsAcquired.Should().BeTrue();
        result.ExistingRecord.Should().BeNull();
    }

    [Fact]
    public async Task TryAcquireOrGetAsync_WhenKeyAlreadyInFlight_ReturnsConflictWithInFlightRecord()
    {
        var cacheKey = "idempotency:app1:key-2";
        var fingerprint = "fingerprint-abc";
        var ttl = TimeSpan.FromMinutes(2);

        var firstResult = await sut.TryAcquireOrGetAsync(cacheKey, fingerprint, ttl);
        var secondResult = await sut.TryAcquireOrGetAsync(cacheKey, fingerprint, ttl);

        firstResult.IsAcquired.Should().BeTrue();
        secondResult.IsAcquired.Should().BeFalse();
        secondResult.ExistingRecord.Should().NotBeNull();
        secondResult.ExistingRecord!.Status.Should().Be(IdempotencyStatus.InFlight);
        secondResult.ExistingRecord.RequestFingerprint.Should().Be(fingerprint);
    }

    [Fact]
    public async Task SaveCompletedAsync_WhenSaved_UpdatesStatusToCompletedAndPersistsPayload()
    {
        var cacheKey = "idempotency:app1:key-3";
        var fingerprint = "fingerprint-xyz";
        var inFlightTtl = TimeSpan.FromMinutes(2);
        var completedTtl = TimeSpan.FromHours(24);
        var responseJson = "{\"id\":\"123\",\"success\":true}";
        var headers = new Dictionary<string, string> { ["Location"] = "/api/v1/items/123" };

        await sut.TryAcquireOrGetAsync(cacheKey, fingerprint, inFlightTtl);
        await sut.SaveCompletedAsync(cacheKey, fingerprint, 201, responseJson, headers, completedTtl);

        var checkResult = await sut.TryAcquireOrGetAsync(cacheKey, fingerprint, inFlightTtl);

        checkResult.IsAcquired.Should().BeFalse();
        checkResult.ExistingRecord.Should().NotBeNull();
        checkResult.ExistingRecord!.Status.Should().Be(IdempotencyStatus.Completed);
        checkResult.ExistingRecord.StatusCode.Should().Be(201);
        checkResult.ExistingRecord.ResponseJson.Should().Be(responseJson);
        checkResult.ExistingRecord.Headers.Should().ContainKey("Location").WhoseValue.Should().Be("/api/v1/items/123");
    }

    [Fact]
    public async Task RemoveAsync_WhenCalled_EvictsKeyAllowingReacquisition()
    {
        var cacheKey = "idempotency:app1:key-4";
        var fingerprint = "fingerprint-test";
        var ttl = TimeSpan.FromMinutes(2);

        var firstAcquire = await sut.TryAcquireOrGetAsync(cacheKey, fingerprint, ttl);
        firstAcquire.IsAcquired.Should().BeTrue();

        await sut.RemoveAsync(cacheKey);

        var reacquire = await sut.TryAcquireOrGetAsync(cacheKey, fingerprint, ttl);
        reacquire.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task Concurrency_MultipleThreadsAttemptAcquire_ExactlyOneWins()
    {
        var cacheKey = "idempotency:app1:concurrent-key";
        var fingerprint = "fingerprint-concurrent";
        var ttl = TimeSpan.FromMinutes(2);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => sut.TryAcquireOrGetAsync(cacheKey, fingerprint, ttl)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var acquiredCount = results.Count(r => r.IsAcquired);
        var conflictCount = results.Count(r => !r.IsAcquired);

        acquiredCount.Should().Be(1);
        conflictCount.Should().Be(19);
    }
}
