using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Factories;

public static class TemplateDatasetTestFactory
{
    public static TemplateDataset Create(
        Guid? id = null,
        Guid? templateId = null,
        Guid? datasetId = null,
        DatasetAlias? alias = null,
        int sortOrder = 1,
        DateTimeOffset? now = null)
    {
        return new TemplateDataset(
            id ?? Guid.NewGuid(),
            templateId ?? Guid.NewGuid(),
            datasetId ?? Guid.NewGuid(),
            alias ?? DatasetAlias.Create("default_alias"),
            sortOrder,
            now ?? TestConstants.BaselineTime);
    }
}
