using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain join entity linking a template to a dataset with an alias for expression resolution.
/// </summary>
public class TemplateDataset : BaseEntity
{
    public Guid TemplateId { get; private set; }
    public Guid DatasetId { get; private set; }

    /// <summary>Short identifier used in FieldMapping.DatasetAlias, e.g. "customer" or "items".</summary>
    public string Alias { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    // Navigation
    public virtual Template? Template { get; private set; }
    public virtual Dataset? Dataset { get; private set; }

    // For EF Core materialization
    private TemplateDataset() { }

    public TemplateDataset(Guid templateId, Guid datasetId, string alias, int sortOrder, Guid? id = null)
        : base(id)
    {
        if (templateId == Guid.Empty)
        {
            throw new DomainValidationException("TemplateId cannot be empty.");
        }

        if (datasetId == Guid.Empty)
        {
            throw new DomainValidationException("DatasetId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new DomainValidationException("Alias cannot be empty or whitespace.");
        }

        TemplateId = templateId;
        DatasetId = datasetId;
        Alias = alias.Trim();
        SortOrder = sortOrder;
    }

    public void UpdateAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new DomainValidationException("Alias cannot be empty or whitespace.");
        }

        Alias = alias.Trim();
        SetUpdated();
    }
}
