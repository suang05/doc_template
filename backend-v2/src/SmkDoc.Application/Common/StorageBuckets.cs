using System.Diagnostics.CodeAnalysis;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common;

public static class StorageBuckets
{
    public const string Templates = "templates";
    public const string Drafts = "drafts";
    public const string Outputs = "outputs";
    public const string AuditPayloads = "audit-payloads";
    public const string Fonts = "fonts";

    public static string TemplateVersionKey(ProjectId projectId, TemplateId templateId, int version, string extensionWithDot) =>
        $"templates/{projectId.Value:N}/{templateId.Value:N}/v{version}{NormalizeExtension(extensionWithDot)}";

    public static string DraftTemplateKey(ProjectId projectId, string draftId, string extensionWithDot) =>
        $"drafts/{projectId.Value:N}/{draftId}{NormalizeExtension(extensionWithDot)}";

    public static string TenantFontKey(ProjectId projectId, string safeFileName) =>
        $"fonts/{projectId.Value:N}/{safeFileName}";

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Canonical lowercase object storage key extension.")]
    private static string NormalizeExtension(string extensionWithDot) =>
        extensionWithDot.StartsWith('.')
            ? extensionWithDot.ToLowerInvariant()
            : $".{extensionWithDot.ToLowerInvariant()}";
}
