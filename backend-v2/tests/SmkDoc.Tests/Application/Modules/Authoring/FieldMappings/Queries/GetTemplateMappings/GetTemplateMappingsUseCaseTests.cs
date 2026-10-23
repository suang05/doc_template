using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;

public sealed class GetTemplateMappingsUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();

    private GetTemplateMappingsUseCase CreateSut() =>
        new(_templateRepo);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_ReturnsSortedMappings()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("Invoice")
            .WithSlug("invoice")
            .Build();
        var m1 = new FieldMappingBuilder()
            .WithTemplateId(templateId)
            .WithPlaceholder("total")
            .WithSourcePath("payment.total")
            .WithLabel("ยอดชำระ")
            .WithRequired(false)
            .WithSortOrder(2)
            .WithDataSourceType(DataSourceType.Json)
            .Build();
        var m2 = new FieldMappingBuilder()
            .WithTemplateId(templateId)
            .WithPlaceholder("name")
            .WithSourcePath("customer.name")
            .WithLabel("ชื่อลูกค้า")
            .WithRequired(false)
            .WithSortOrder(1)
            .WithDataSourceType(DataSourceType.Json)
            .Build();
        var mappings = new List<FieldMapping> { m1, m2 };
        template.ReplaceFieldMappings(mappings, TestConstants.BaselineTime);

        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetTemplateMappingsQuery(templateId));

        // Assert
        result.Should().HaveCount(2);
        result[0].Placeholder.Should().Be("name");
        result[1].Placeholder.Should().Be("total");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetTemplateMappingsQuery(templateId));

        // Assert
        result.Should().BeEmpty();
    }
}
