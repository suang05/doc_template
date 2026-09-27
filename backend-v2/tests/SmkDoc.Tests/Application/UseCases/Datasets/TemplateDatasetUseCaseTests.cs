using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.UseCases.FieldMappings;
using SmkDoc.Domain.Entities;
using Xunit;

namespace SmkDoc.Tests;

public class TemplateDatasetUseCaseTests
{
    private readonly Mock<IRepository<TemplateDataset>> _tdRepoMock = new();
    private readonly Mock<IRepository<Template>> _templateRepoMock = new();
    private readonly Mock<IRepository<Dataset>> _datasetRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private TemplateDatasetUseCase CreateSut() =>
        new(_tdRepoMock.Object, _templateRepoMock.Object, _datasetRepoMock.Object, _unitOfWorkMock.Object);

    // ── GetByTemplateIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByTemplateIdAsync_ShouldReturnEnrichedDto_OrderedBySortOrder()
    {
        var templateId = Guid.NewGuid();
        var dsId1 = Guid.NewGuid();
        var dsId2 = Guid.NewGuid();

        var rows = new List<TemplateDataset>
        {
            new() { Id = Guid.NewGuid(), TemplateId = templateId, DatasetId = dsId2, Alias = "items", SortOrder = 2 },
            new() { Id = Guid.NewGuid(), TemplateId = templateId, DatasetId = dsId1, Alias = "header", SortOrder = 1 }
        };

        var datasets = new List<Dataset>
        {
            new() { Id = dsId1, Name = "Invoice Header" },
            new() { Id = dsId2, Name = "Invoice Items" }
        };

        _tdRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateDataset, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        _datasetRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<Dataset, bool>>>(), It.IsAny<CancellationToken>()))
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
    public async Task SaveAsync_ShouldThrowKeyNotFoundException_WhenTemplateDoesNotExist()
    {
        var templateId = Guid.NewGuid();
        _templateRepoMock.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var sut = CreateSut();
        var act = () => sut.SaveAsync(templateId, new List<SaveTemplateDatasetItem>());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{templateId}*");
    }

    [Fact]
    public async Task SaveAsync_ShouldThrowInvalidOperationException_WhenDuplicateAliasesProvided()
    {
        var templateId = Guid.NewGuid();
        _templateRepoMock.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Name = "Contract" });

        var items = new List<SaveTemplateDatasetItem>
        {
            new(Guid.NewGuid(), "orders", 1),
            new(Guid.NewGuid(), "ORDERS ", 2) // Duplicate case-insensitive
        };

        var sut = CreateSut();
        var act = () => sut.SaveAsync(templateId, items);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*aliases must be unique*");
    }

    [Fact]
    public async Task SaveAsync_ShouldReplaceOldAssignmentsAndPersistNew()
    {
        var templateId = Guid.NewGuid();
        _templateRepoMock.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Name = "Receipt" });

        var existingOld = new List<TemplateDataset>
        {
            new() { Id = Guid.NewGuid(), TemplateId = templateId, Alias = "old_alias" }
        };

        _tdRepoMock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateDataset, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOld);

        var addedItems = new List<TemplateDataset>();
        _tdRepoMock.Setup(r => r.AddAsync(It.IsAny<TemplateDataset>(), It.IsAny<CancellationToken>()))
            .Callback<TemplateDataset, CancellationToken>((td, _) => addedItems.Add(td))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dsId = Guid.NewGuid();
        var newItems = new List<SaveTemplateDatasetItem>
        {
            new(dsId, "payments", 1)
        };

        var sut = CreateSut();
        await sut.SaveAsync(templateId, newItems);

        _tdRepoMock.Verify(r => r.Remove(existingOld[0]), Times.Once);
        addedItems.Should().HaveCount(1);
        addedItems[0].Alias.Should().Be("payments");
        addedItems[0].DatasetId.Should().Be(dsId);
        addedItems[0].TemplateId.Should().Be(templateId);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
