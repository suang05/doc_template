using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public class FieldMappingTests
{
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly DateTimeOffset _initialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var mapping = FieldMapping.Create(
            _templateId,
            "customer_name",
            "customer.name",
            "Customer Name",
            required: true,
            sortOrder: 1,
            _initialTime,
            DataSourceType.Json,
            defaultValue: "N/A",
            transform: "uppercase");

        mapping.Id.Should().NotBe(Guid.Empty);
        mapping.TemplateId.Should().Be(_templateId);
        mapping.Placeholder.Should().Be("customer_name");
        mapping.SourcePath.Should().Be("customer.name");
        mapping.Label.Should().Be("Customer Name");
        mapping.Required.Should().BeTrue();
        mapping.SortOrder.Should().Be(1);
        mapping.DataSourceType.Should().Be(DataSourceType.Json);
        mapping.DefaultValue.Should().Be("N/A");
        mapping.Transform.Should().Be("uppercase");
        mapping.CreatedAt.Should().Be(_initialTime);
        mapping.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyPlaceholder_ThrowsDomainValidationException()
    {
        var act = () => FieldMapping.Create(_templateId, "", "path", "label", false, 1, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Placeholder cannot be empty*");
    }

    [Fact]
    public void UpdateMappingDetails_UpdatesFieldsAndAuditTimestamp()
    {
        var mapping = FieldMapping.Create(_templateId, "price", "p", "Price", false, 1, _initialTime);
        var updateTime = _initialTime.AddHours(1);

        mapping.UpdateMappingDetails("order.price", "Order Price", true, "0.00", "baht", 5, updateTime);

        mapping.SourcePath.Should().Be("order.price");
        mapping.Label.Should().Be("Order Price");
        mapping.Required.Should().BeTrue();
        mapping.DefaultValue.Should().Be("0.00");
        mapping.Transform.Should().Be("baht");
        mapping.SortOrder.Should().Be(5);
        mapping.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void ConfigureDataSource_WithSqlSource_UpdatesDataSourceDetails()
    {
        var mapping = FieldMapping.Create(_templateId, "total", "t", "Total", false, 1, _initialTime);
        var updateTime = _initialTime.AddHours(2);

        mapping.ConfigureDataSource(DataSourceType.Sql, DatasetAlias.Create("orders"), "data.total", null, updateTime);

        mapping.DataSourceType.Should().Be(DataSourceType.Sql);
        mapping.DatasetAlias.Should().NotBeNull();
        mapping.DatasetAlias!.Value.Should().Be("orders");
        mapping.ResultPath.Should().Be("data.total");
        mapping.MathExpression.Should().BeNull();
        mapping.UpdatedAt.Should().Be(updateTime);
    }
}
