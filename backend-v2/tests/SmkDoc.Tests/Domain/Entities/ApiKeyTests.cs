using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class ApiKeyTests
{
    private static readonly Sha256Hash ValidHash = new(new string('a', 64));
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_WithValidParameters_CreatesActiveUsableKey()
    {
        var projectId = Guid.NewGuid();
        var name = ApiKeyName.Create("Backend Worker");
        var expiresAt = FixedNow.AddDays(30);
        var expiration = ExpirationPolicy.Until(expiresAt, FixedNow);

        var key = ApiKey.Issue(projectId, name, "worker-service", ValidHash, expiration, FixedNow);

        key.Id.Should().NotBeEmpty();
        key.ProjectId.Should().Be(projectId);
        key.Name.Should().Be(name);
        key.CallerApp.Should().Be("worker-service");
        key.KeyHash.Should().Be(ValidHash);
        key.IsActive.Should().BeTrue();
        key.IsRevoked.Should().BeFalse();
        key.Scope.Should().Be(ApiKeyScope.ReadWrite);
        key.Expiration.Should().Be(expiration);
        key.ExpiresAt.Should().Be(expiresAt);
        key.IsExpiredAt(FixedNow).Should().BeFalse();
        key.IsUsableAt(FixedNow).Should().BeTrue();
        key.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public void Issue_WithReadOnlyScope_SetsScopeCorrectly()
    {
        var projectId = Guid.NewGuid();
        var name = ApiKeyName.Create("Reader Worker");
        var key = ApiKey.Issue(projectId, name, "reader-service", ValidHash, ExpirationPolicy.Never, FixedNow, ApiKeyScope.ReadOnly);

        key.Scope.Should().Be(ApiKeyScope.ReadOnly);
    }

    [Fact]
    public void Issue_WithExpirationInPast_ThrowsDomainValidationException()
    {
        var pastDate = FixedNow.AddMinutes(-5);

        var act = () => ExpirationPolicy.Until(pastDate, FixedNow);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*expiration must be in the future*");
    }

    [Fact]
    public void Issue_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => ApiKey.Issue(Guid.Empty, ApiKeyName.Create("Key"), "app", ValidHash, ExpirationPolicy.Never, FixedNow);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public void RecordUsage_WhenUsable_UpdatesLastUsedAt()
    {
        var key = ApiKey.Issue(Guid.NewGuid(), ApiKeyName.Create("Key"), "app", ValidHash, ExpirationPolicy.Never, FixedNow);
        var usageTime = FixedNow.AddHours(2);

        key.RecordUsage(usageTime);

        key.LastUsedAt.Should().Be(usageTime);
        key.UpdatedAt.Should().Be(usageTime);
    }

    [Fact]
    public void RecordUsage_WhenRevoked_ThrowsApiKeyRevokedException()
    {
        var key = ApiKey.Issue(Guid.NewGuid(), ApiKeyName.Create("Key"), "app", ValidHash, ExpirationPolicy.Never, FixedNow);
        key.Revoke(FixedNow.AddMinutes(5));

        var act = () => key.RecordUsage(FixedNow.AddMinutes(10));

        act.Should().Throw<ApiKeyRevokedException>()
            .Which.ErrorCode.Should().Be("API_KEY_REVOKED");
    }

    [Fact]
    public void RecordUsage_WhenExpired_ThrowsApiKeyExpiredException()
    {
        var expiresAt = FixedNow.AddHours(1);
        var expiration = ExpirationPolicy.Until(expiresAt, FixedNow);
        var key = ApiKey.Issue(Guid.NewGuid(), ApiKeyName.Create("Key"), "app", ValidHash, expiration, FixedNow);

        var act = () => key.RecordUsage(expiresAt.AddSeconds(1));

        act.Should().Throw<ApiKeyExpiredException>()
            .Which.ErrorCode.Should().Be("API_KEY_EXPIRED");
    }

    [Fact]
    public void Rename_WhenActive_UpdatesName()
    {
        var key = ApiKey.Issue(Guid.NewGuid(), ApiKeyName.Create("Old Name"), "app", ValidHash, ExpirationPolicy.Never, FixedNow);
        var newName = ApiKeyName.Create("New Name");

        key.Rename(newName, FixedNow.AddMinutes(1));

        key.Name.Should().Be(newName);
        key.UpdatedAt.Should().Be(FixedNow.AddMinutes(1));
    }

    [Fact]
    public void Rename_WhenRevoked_ThrowsApiKeyRevokedException()
    {
        var key = ApiKey.Issue(Guid.NewGuid(), ApiKeyName.Create("Name"), "app", ValidHash, ExpirationPolicy.Never, FixedNow);
        key.Revoke(FixedNow);

        var act = () => key.Rename(ApiKeyName.Create("Another Name"), FixedNow.AddMinutes(1));

        act.Should().Throw<ApiKeyRevokedException>();
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        var key = ApiKey.Issue(Guid.NewGuid(), ApiKeyName.Create("Key"), "app", ValidHash, ExpirationPolicy.Never, FixedNow);
        var firstRevoke = FixedNow.AddMinutes(10);
        key.Revoke(firstRevoke);

        key.IsActive.Should().BeFalse();
        key.IsRevoked.Should().BeTrue();
        key.UpdatedAt.Should().Be(firstRevoke);

        // Second revoke must not throw and must not alter timestamp
        key.Revoke(FixedNow.AddMinutes(20));
        key.UpdatedAt.Should().Be(firstRevoke);
    }
}
