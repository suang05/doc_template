namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to add or replace a field mapping with a placeholder that already exists in the template.
/// </summary>
public sealed class DuplicatePlaceholderException(Guid templateId, string placeholder)
    : BusinessRuleViolationException($"FieldMapping with placeholder '{placeholder}' already exists in template '{templateId}'.", "DUPLICATE_PLACEHOLDER")
{
    public Guid TemplateId { get; } = templateId;
    public string Placeholder { get; } = placeholder;
}
