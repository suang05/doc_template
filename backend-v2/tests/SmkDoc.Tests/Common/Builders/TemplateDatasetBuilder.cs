using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class TemplateDatasetBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _templateId = Guid.NewGuid();
    private Guid _datasetId = Guid.NewGuid();
    private string _alias = "default_alias";
    private int _sortOrder = 1;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static TemplateDatasetBuilder ADatasetLink() => new();

    public static TemplateDataset Default() => ADatasetLink().Build();

    public static TemplateDataset Create(
        Guid? id = null,
        Guid? templateId = null,
        Guid? datasetId = null,
        DatasetAlias? alias = null,
        int sortOrder = 1,
        DateTimeOffset? now = null)
    {
        var builder = new TemplateDatasetBuilder();
        if (id.HasValue) builder.WithId(id.Value);
        if (templateId.HasValue) builder.WithTemplateId(templateId.Value);
        if (datasetId.HasValue) builder.WithDatasetId(datasetId.Value);
        if (alias != null) builder.WithAlias(alias.Value);
        builder.WithSortOrder(sortOrder);
        if (now.HasValue) builder.WithTime(now.Value);
        return builder.Build();
    }

    public TemplateDatasetBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TemplateDatasetBuilder WithTemplateId(Guid templateId)
    {
        _templateId = templateId;
        return this;
    }

    public TemplateDatasetBuilder WithDatasetId(Guid datasetId)
    {
        _datasetId = datasetId;
        return this;
    }

    public TemplateDatasetBuilder WithAlias(string alias)
    {
        _alias = alias;
        return this;
    }

    public TemplateDatasetBuilder WithSortOrder(int sortOrder)
    {
        _sortOrder = sortOrder;
        return this;
    }

    public TemplateDatasetBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public TemplateDataset Build()
    {
        return new TemplateDataset(
            _id,
            _templateId,
            _datasetId,
            DatasetAlias.Create(_alias),
            _sortOrder,
            _now);
    }
}
