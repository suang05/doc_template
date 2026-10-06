using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;

public class GetTemplateMappingsUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepoMock = new();

    private GetTemplateMappingsUseCase CreateSut() =>
        new(_templateRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_ReturnsSortedMappings()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "Invoice", "invoice");
        var m1 = FieldMapping.Create(templateId, "total", "payment.total", "ยอดชำระ", false, 2, TestConstants.BaselineTime, DataSourceType.Json);
        var m2 = FieldMapping.Create(templateId, "name", "customer.name", "ชื่อลูกค้า", false, 1, TestConstants.BaselineTime, DataSourceType.Json);
        var mappings = new List<FieldMapping> { m1, m2 };
        template.ReplaceFieldMappings(mappings, TestConstants.BaselineTime);

        _templateRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

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
        _templateRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetTemplateMappingsQuery(templateId));

        // Assert
        result.Should().BeEmpty();
    }
}
