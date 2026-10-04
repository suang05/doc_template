using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Integration.Datasets;

public class TemplateDatasetUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepoMock = new();
    private readonly Mock<IDatasetRepository> _datasetRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private TemplateDatasetUseCase CreateSut() =>
        new(_templateRepoMock.Object, _datasetRepoMock.Object, _unitOfWorkMock.Object);

    // ── GetByTemplateIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByTemplateIdAsync_ShouldReturnEnrichedDto_OrderedBySortOrder()
    {
        var templateId = Guid.NewGuid();
        var dsId1 = Guid.NewGuid();
        var dsId2 = Guid.NewGuid();

        var rows = new List<TemplateDataset>
        {
            new TemplateDataset(templateId, dsId2, "items", 2),
            new TemplateDataset(templateId, dsId1, "header", 1)
        };

        var datasets = new List<Dataset>
        {
            new Dataset("Invoice Header", null, Guid.NewGuid(), "sql", 0, id: dsId1),
            new Dataset("Invoice Items", null, Guid.NewGuid(), "sql", 0, id: dsId2)
        };

        var template = new Template(Guid.NewGuid(), "Invoice", "invoice", null, id: templateId);
        template.ReplaceDatasets(rows);
        _templateRepoMock.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _datasetRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasets);

        var sut = CreateSut();
        var result = await sut.GetByTemplateIdAsync(templateId);

        result.Should().HaveCount(2);
        result[0].Alias.Should().Be("header");
        result[0].DatasetName.Should().Be("Invoice Header");
        result[0].SortOrder.Should().Be(1);

        result[1].Alias.Should().Be("items");
        result[1].DatasetName.Should().Be("Invoice Items");
        result[1].SortOrder.Should().Be(2);
    }

    // ── SaveAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ShouldThrowNotFoundException_WhenTemplateDoesNotExist()
    {
        var templateId = Guid.NewGuid();
        _templateRepoMock.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var sut = CreateSut();
        Func<Task> act = () => sut.SaveAsync(templateId, new List<SaveTemplateDatasetItemDto>());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{templateId}*");
    }

    [Fact]
    public async Task SaveAsync_ShouldThrowBusinessRuleViolationException_WhenDuplicateAliasesProvided()
    {
        var templateId = Guid.NewGuid();
        _templateRepoMock.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template(Guid.NewGuid(), "Contract", "contract", null, id: templateId));

        var items = new List<SaveTemplateDatasetItemDto>
        {
            new(Guid.NewGuid(), "orders", 1),
            new(Guid.NewGuid(), "ORDERS ", 2) // Duplicate case-insensitive
        };

        var sut = CreateSut();
        Func<Task> act = () => sut.SaveAsync(templateId, items);

        var ex = await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .WithMessage("*aliases must be unique*");
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
    }

    [Fact]
    public async Task SaveAsync_ShouldReplaceOldAssignmentsAndPersistNew()
    {
        var templateId = Guid.NewGuid();
        var template = new Template(Guid.NewGuid(), "Receipt", "receipt", null, id: templateId);
        template.AttachDataset(Guid.NewGuid(), "old_alias", 1);

        _templateRepoMock.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _unitOfWorkMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dsId = Guid.NewGuid();
        var newItems = new List<SaveTemplateDatasetItemDto>
        {
            new(dsId, "payments", 1)
        };

        var sut = CreateSut();
        await sut.SaveAsync(templateId, newItems);

        template.TemplateDatasets.Should().HaveCount(1);
        template.TemplateDatasets.First().Alias.Should().Be("payments");
        template.TemplateDatasets.First().DatasetId.Should().Be(dsId);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
