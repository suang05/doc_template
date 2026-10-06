using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

/// <summary>
/// Test factory for creating Template entities with arbitrary state in tests.
/// Kept strictly inside SmkDoc.Tests to prevent test-specific code from polluting the production Domain.
/// </summary>
public static class TemplateTestFactory
{
    public static Template Create(
        Guid? id = null,
        Guid? projectId = null,
        string name = "Sample Template",
        string slug = "sample-template",
        string? category = null,
        DateTimeOffset? now = null,
        bool isActive = true,
        Guid? currentVersionId = null)
    {
        var timestamp = now ?? TestConstants.BaselineTime;
        var template = new Template(
            id ?? Guid.NewGuid(),
            projectId ?? Guid.NewGuid(),
            TemplateName.Create(name),
            TemplateSlug.Create(slug),
            category,
            timestamp);

        if (currentVersionId.HasValue)
        {
            template.SetCurrentVersion(currentVersionId.Value, timestamp);
        }

        if (!isActive)
        {
            template.Deactivate(timestamp);
        }

        return template;
    }
}
