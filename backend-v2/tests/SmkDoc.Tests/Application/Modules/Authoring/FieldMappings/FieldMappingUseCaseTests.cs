using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings;

public class FieldMappingUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    [Fact]
    public async Task GetMappingsByTemplateIdAsync_ShouldReturnSortedMappings()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "Invoice", "invoice");
        var m1 = FieldMapping.Create(templateId, "total", "payment.total", "ยอดชำระ", false, 2, now, DataSourceType.Json);
        var m2 = FieldMapping.Create(templateId, "name", "customer.name", "ชื่อลูกค้า", false, 1, now, DataSourceType.Json);
        var mappings = new List<FieldMapping> { m1, m2 };
        template.ReplaceFieldMappings(mappings, now);

        _mockTemplateRepo.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = new GetTemplateMappingsUseCase(_mockTemplateRepo.Object);

        // Act
        var result = await useCase.ExecuteAsync(new GetTemplateMappingsQuery(templateId));

        // Assert
        result.Should().HaveCount(2);
        result[0].Placeholder.Should().Be("name");
        result[1].Placeholder.Should().Be("total");
    }

    [Fact]
    public async Task SaveMappingsAsync_ShouldReplaceOldMappings_WithNewOnes()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "Invoice", "invoice");

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = new SaveTemplateMappingsUseCase(_mockTemplateRepo.Object, _mockUow.Object);

        var items = new List<SaveFieldMappingItemDto>
        {
            new("amount", "payment.amount", "จำนวนเงิน", true, "0", "thai_baht_text", 1)
        };

        // Act
        await useCase.ExecuteAsync(new SaveTemplateMappingsCommand(templateId, items));

        // Assert
        template.FieldMappings.Should().HaveCount(1);
        var mapping = template.FieldMappings.First();
        mapping.TemplateId.Should().Be(templateId);
        mapping.Placeholder.Should().Be("amount");
        mapping.Transform.Should().Be("thai_baht_text");
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
