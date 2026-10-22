using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class TemplateDatasetTests
{
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _datasetId = Guid.NewGuid();
    private readonly DateTimeOffset _initialTime = TestConstants.BaselineTime;

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var td = TemplateDataset.Create(_templateId, _datasetId, DatasetAlias.Create("customer_ds"), 1, _initialTime);

        td.Id.Should().NotBe(Guid.Empty);
        td.TemplateId.Should().Be(_templateId);
        td.DatasetId.Should().Be(_datasetId);
        td.Alias.Value.Should().Be("customer_ds");
        td.SortOrder.Should().Be(1);
        td.CreatedAt.Should().Be(_initialTime);
        td.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyTemplateId_ThrowsDomainValidationException()
    {
        var act = () => TemplateDataset.Create(Guid.Empty, _datasetId, DatasetAlias.Create("alias"), 1, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*TemplateId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyDatasetId_ThrowsDomainValidationException()
    {
        var act = () => TemplateDataset.Create(_templateId, Guid.Empty, DatasetAlias.Create("alias"), 1, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*DatasetId cannot be empty*");
    }

    [Fact]
    public void UpdateAlias_WhenValid_MutatesAliasAndUpdatesTimestamp()
    {
        var td = TemplateDataset.Create(_templateId, _datasetId, DatasetAlias.Create("old_alias"), 1, _initialTime);
        var updateTime = _initialTime.AddMinutes(15);

        td.UpdateAlias(DatasetAlias.Create("new_alias"), updateTime);

        td.Alias.Value.Should().Be("new_alias");
        td.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void UpdateSortOrder_WhenValid_MutatesSortOrderAndUpdatesTimestamp()
    {
        var td = TemplateDataset.Create(_templateId, _datasetId, DatasetAlias.Create("alias"), 1, _initialTime);
        var updateTime = _initialTime.AddMinutes(10);

        td.UpdateSortOrder(5, updateTime);

        td.SortOrder.Should().Be(5);
        td.UpdatedAt.Should().Be(updateTime);
    }
}
