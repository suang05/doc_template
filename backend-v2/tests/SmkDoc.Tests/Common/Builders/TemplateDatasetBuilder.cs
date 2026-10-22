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

    public static implicit operator TemplateDataset(TemplateDatasetBuilder builder) => builder.Build();
}
