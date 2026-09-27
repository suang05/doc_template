namespace SmkDoc.Domain.Entities;

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

    private TemplateDataset() { }

    public TemplateDataset(Guid templateId, Guid datasetId, string alias, int sortOrder)
    {
        TemplateId = templateId;
        DatasetId = datasetId;
        Alias = alias;
        SortOrder = sortOrder;
    }

    public void UpdateAlias(string alias)
    {
        Alias = alias;
        SetUpdated();
    }
}
