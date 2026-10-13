using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain join entity linking a template to a dataset with an alias for expression resolution.
/// </summary>
public sealed class TemplateDataset : BaseEntity
{
    public TemplateId TemplateId { get; private set; }
    public DatasetId DatasetId { get; private set; }
    public DatasetAlias Alias { get; private set; } = null!;
    public int SortOrder { get; private set; }

    // Navigation property for EF Core materialization
    public Template? Template { get; private set; }

    // For EF Core materialization only
    private TemplateDataset() { }

    internal TemplateDataset(
        Guid? id,
        TemplateId templateId,
        DatasetId datasetId,
        DatasetAlias alias,
        int sortOrder,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        TemplateId = Guard.NotEmpty(templateId, nameof(TemplateId));
        DatasetId = Guard.NotEmpty(datasetId, nameof(DatasetId));
        Alias = Guard.NotNull(alias, nameof(Alias));
        SortOrder = sortOrder;
    }

    public static TemplateDataset Create(
        TemplateId templateId,
        DatasetId datasetId,
        DatasetAlias alias,
        int sortOrder,
        DateTimeOffset now) =>
        new(null, templateId, datasetId, alias, sortOrder, now);

    public void UpdateAlias(DatasetAlias alias, DateTimeOffset now)
    {
        Alias = Guard.NotNull(alias, nameof(Alias));
        SetUpdated(now);
    }

    public void UpdateSortOrder(int sortOrder, DateTimeOffset now)
    {
        SortOrder = sortOrder;
        SetUpdated(now);
    }
}
