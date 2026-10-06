using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

/// <summary>
/// Test factory for creating Project entities with arbitrary state in tests.
/// Kept strictly inside SmkDoc.Tests to prevent test-specific code from polluting the production Domain.
/// </summary>
public static class ProjectTestFactory
{
    public static Project Create(
        Guid? id = null,
        Guid? companyId = null,
        string name = "Sample Project",
        string slug = "sample-project",
        DateTimeOffset? now = null,
        bool isActive = true)
    {
        var timestamp = now ?? TestConstants.BaselineTime;
        var project = new Project(
            id ?? Guid.NewGuid(),
            companyId ?? Guid.NewGuid(),
            ProjectName.Create(name),
            TemplateSlug.Create(slug),
            timestamp);

        if (!isActive)
        {
            project.Deactivate(timestamp);
        }

        return project;
    }
}
