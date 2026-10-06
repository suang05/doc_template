namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to assign a current version that does not belong to the template.
/// </summary>
public sealed class VersionNotInTemplateException(Guid templateId, Guid versionId)
    : BusinessRuleViolationException($"Version '{versionId}' does not belong to template '{templateId}'.", "VERSION_NOT_IN_TEMPLATE")
{
    public Guid TemplateId { get; } = templateId;
    public Guid VersionId { get; } = versionId;
}
