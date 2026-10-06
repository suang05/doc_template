namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to add a version number that already exists in a template.
/// </summary>
public sealed class DuplicateVersionException(Guid templateId, int version)
    : BusinessRuleViolationException($"Version {version} already exists in template '{templateId}'.", "DUPLICATE_VERSION")
{
    public Guid TemplateId { get; } = templateId;
    public int Version { get; } = version;
}
