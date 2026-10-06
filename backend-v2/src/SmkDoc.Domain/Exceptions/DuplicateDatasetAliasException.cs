namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to attach a dataset with an alias already in use by the template.
/// </summary>
public sealed class DuplicateDatasetAliasException(Guid templateId, string alias)
    : BusinessRuleViolationException($"Dataset alias '{alias}' is already assigned to template '{templateId}'.", "DUPLICATE_DATASET_ALIAS")
{
    public Guid TemplateId { get; } = templateId;
    public string Alias { get; } = alias;
}
