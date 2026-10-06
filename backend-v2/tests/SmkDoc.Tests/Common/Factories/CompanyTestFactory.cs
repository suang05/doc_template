using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

/// <summary>
/// Test factory for creating Company entities with arbitrary state in tests.
/// Kept strictly inside SmkDoc.Tests to prevent test-specific code from polluting the production Domain.
/// </summary>
public static class CompanyTestFactory
{
    public static Company Create(
        Guid? id = null,
        string name = "Acme Corp",
        DateTimeOffset? now = null,
        bool isActive = true)
    {
        var timestamp = now ?? TestConstants.BaselineTime;
        var company = new Company(
            id ?? Guid.NewGuid(),
            CompanyName.Create(name),
            timestamp);

        if (!isActive)
        {
            company.Deactivate(timestamp);
        }

        return company;
    }
}
