using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

/// <summary>
/// Test factory for creating Dataset entities with arbitrary state in tests.
/// Kept strictly inside SmkDoc.Tests to prevent test-specific code from polluting the production Domain.
/// </summary>
public static class DatasetTestFactory
{
    public static Dataset Create(
        Guid? id = null,
        string name = "Default Dataset",
        string? description = null,
        Guid? dataConnectionId = null,
        string sqlQuery = "SELECT 1",
        int cacheSeconds = 0,
        DateTimeOffset? now = null)
    {
        var timestamp = now ?? TestConstants.BaselineTime;
        return new Dataset(
            id ?? Guid.NewGuid(),
            DatasetName.Create(name),
            description,
            dataConnectionId ?? Guid.NewGuid(),
            sqlQuery,
            cacheSeconds,
            timestamp);
    }
}
