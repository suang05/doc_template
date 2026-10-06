using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Tests.Common.Factories;

public static class TemplateVersionTestFactory
{
    public static TemplateVersion Create(
        Guid? id = null,
        Guid? templateId = null,
        int version = 1,
        string storageKey = "templates/sample.html",
        TemplateFormat? format = null,
        string? status = "Published",
        string? commitMessage = "Initial commit",
        DateTimeOffset? now = null,
        string? createdBy = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var tv = new TemplateVersion(
            id ?? Guid.NewGuid(),
            templateId ?? Guid.NewGuid(),
            version,
            storageKey,
            format ?? TemplateFormat.Html,
            createdBy,
            timestamp,
            commitMessage);

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase))
            {
                tv.Publish(timestamp);
            }
            else if (string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase))
            {
                tv.Archive(timestamp);
            }
        }

        return tv;
    }
}
