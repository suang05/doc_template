using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

/// <summary>
/// Test factory for creating DataConnection entities with arbitrary state in tests.
/// Kept strictly inside SmkDoc.Tests to prevent test-specific code from polluting the production Domain.
/// </summary>
public static class DataConnectionTestFactory
{
    public static DataConnection Create(
        Guid? id = null,
        string name = "Default Connection",
        DatabaseProvider? provider = null,
        string encryptedConnectionString = "Host=localhost;Database=test",
        DateTimeOffset? now = null)
    {
        var timestamp = now ?? TestConstants.BaselineTime;
        return new DataConnection(
            id ?? Guid.NewGuid(),
            ConnectionName.Create(name),
            provider ?? DatabaseProvider.PostgreSQL,
            encryptedConnectionString,
            timestamp);
    }
}
