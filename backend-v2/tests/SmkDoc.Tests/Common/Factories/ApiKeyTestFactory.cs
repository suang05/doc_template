using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

public static class ApiKeyTestFactory
{
    public static ApiKey Create(
        Guid? id = null,
        Guid? projectId = null,
        ApiKeyName? name = null,
        string? callerApp = "TestApp",
        Sha256Hash? keyHash = null,
        ExpirationPolicy? expiration = null,
        DateTimeOffset? now = null)
    {
        return new ApiKey(
            id ?? Guid.NewGuid(),
            projectId ?? Guid.NewGuid(),
            name ?? ApiKeyName.Create("Test Key"),
            callerApp,
            keyHash ?? Sha256Hash.Create(new string('a', 64)),
            expiration ?? ExpirationPolicy.Never,
            now ?? TestConstants.BaselineTime);
    }
}
