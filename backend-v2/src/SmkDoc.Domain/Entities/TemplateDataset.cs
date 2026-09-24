namespace SmkDoc.Domain.Entities;

public class TemplateDataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public Guid DatasetId { get; set; }

    /// <summary>Short identifier used in FieldMapping.DatasetAlias, e.g. "customer" or "items".</summary>
    public string Alias { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    // Navigation
    public Template? Template { get; set; }
    public Dataset? Dataset { get; set; }
}
