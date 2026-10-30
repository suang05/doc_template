using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common;

public static class StorageBuckets
{
    public const string Templates = "templates";
    public const string Outputs = "outputs";
    public const string AuditPayloads = "audit-payloads";
    public const string Fonts = "fonts";

    public static string TemplateVersionKey(ProjectId projectId, TemplateId templateId, int version, string extensionWithDot)
    {
        var ext = extensionWithDot.StartsWith('.')
            ? extensionWithDot.ToLowerInvariant()
            : $".{extensionWithDot.ToLowerInvariant()}";
        return $"templates/{projectId.Value:N}/{templateId.Value:N}/v{version}{ext}";
    }

    public static string TenantFontKey(ProjectId projectId, string safeFileName) =>
        $"fonts/{projectId.Value:N}/{safeFileName}";
}
