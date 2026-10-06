using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;

public class GetTemplateDatasetsUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepoMock = new();
    private readonly Mock<IDatasetRepository> _datasetRepoMock = new();

    private GetTemplateDatasetsUseCase CreateSut() =>
        new(_templateRepoMock.Object, _datasetRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_ReturnsEnrichedDtosOrderedBySortOrder()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var dsId1 = Guid.NewGuid();
        var dsId2 = Guid.NewGuid();

        var rows = new List<TemplateDataset>
        {
            TemplateDataset.Create(templateId, dsId2, DatasetAlias.Create("items"), 2, TestConstants.BaselineTime),
            TemplateDataset.Create(templateId, dsId1, DatasetAlias.Create("header"), 1, TestConstants.BaselineTime)
        };

        var datasets = new List<Dataset>
        {
            DatasetTestFactory.Create(dsId1, "Invoice Header", null, Guid.NewGuid(), "sql", 0),
            DatasetTestFactory.Create(dsId2, "Invoice Items", null, Guid.NewGuid(), "sql", 0)
        };

        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "Invoice", "invoice");
        template.ReplaceDatasets(rows, TestConstants.BaselineTime);

        _templateRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _datasetRepoMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasets);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetTemplateDatasetsQuery(templateId));

        // Assert
        result.Should().HaveCount(2);
        result[0].Alias.Should().Be("header");
        result[0].DatasetName.Should().Be("Invoice Header");
        result[0].SortOrder.Should().Be(1);

        result[1].Alias.Should().Be("items");
        result[1].DatasetName.Should().Be("Invoice Items");
        result[1].SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetTemplateDatasetsQuery(templateId));

        // Assert
        result.Should().BeEmpty();
    }
}
