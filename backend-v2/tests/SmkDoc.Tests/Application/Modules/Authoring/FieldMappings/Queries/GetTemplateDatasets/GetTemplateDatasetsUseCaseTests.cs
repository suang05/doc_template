using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;

public sealed class GetTemplateDatasetsUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();

    private GetTemplateDatasetsUseCase CreateSut() =>
        new(_templateRepo, _datasetRepo);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_ReturnsEnrichedDtosOrderedBySortOrder()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var dsId1 = Guid.NewGuid();
        var dsId2 = Guid.NewGuid();

        var rows = new List<TemplateDataset>
        {
            new TemplateDatasetBuilder()
                .WithTemplateId(templateId)
                .WithDatasetId(dsId2)
                .WithAlias("items")
                .WithSortOrder(2)
                .WithTime(TestConstants.BaselineTime)
                .Build(),
            new TemplateDatasetBuilder()
                .WithTemplateId(templateId)
                .WithDatasetId(dsId1)
                .WithAlias("header")
                .WithSortOrder(1)
                .WithTime(TestConstants.BaselineTime)
                .Build()
        };

        var datasets = new List<Dataset>
        {
            new DatasetBuilder()
                .WithId(dsId1)
                .WithName("Invoice Header")
                .WithDataConnectionId(Guid.NewGuid())
                .WithSqlQuery("sql")
                .WithCacheSeconds(0)
                .Build(),
            new DatasetBuilder()
                .WithId(dsId2)
                .WithName("Invoice Items")
                .WithDataConnectionId(Guid.NewGuid())
                .WithSqlQuery("sql")
                .WithCacheSeconds(0)
                .Build()
        };

        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("Invoice")
            .WithSlug("invoice")
            .Build();
        template.ReplaceDatasets(rows, TestConstants.BaselineTime);

        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _datasetRepo.GetByIdsAsync(Arg.Any<IEnumerable<DatasetId>>(), Arg.Any<CancellationToken>())
            .Returns(datasets);

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
        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetTemplateDatasetsQuery(templateId));

        // Assert
        result.Should().BeEmpty();
    }
}
