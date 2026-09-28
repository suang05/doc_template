using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings;

public class FieldMappingUseCaseTests
{
    private readonly Mock<IFieldMappingRepository> _mockMappingRepo = new();
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    [Fact]
    public async Task GetMappingsByTemplateIdAsync_ShouldReturnSortedMappings()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var m1 = new FieldMapping(templateId, "total", "payment.total", "ยอดชำระ", false, 2, DataSourceType.Json) { Id = Guid.NewGuid() };
        var m2 = new FieldMapping(templateId, "name", "customer.name", "ชื่อลูกค้า", false, 1, DataSourceType.Json) { Id = Guid.NewGuid() };
        var mappings = new List<FieldMapping> { m1, m2 };

        _mockMappingRepo.Setup(r => r.GetByTemplateIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings);

        var useCase = new FieldMappingUseCase(_mockMappingRepo.Object, _mockTemplateRepo.Object, _mockUow.Object);

        // Act
        var result = await useCase.GetMappingsByTemplateIdAsync(templateId);

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
        var template = new Template(Guid.NewGuid(), "Invoice", "invoice", null) { Id = templateId };
        var existingMapping = new FieldMapping(templateId, "old", "old", "Old", false, 1, DataSourceType.Json) { Id = Guid.NewGuid() };
        var existingList = new List<FieldMapping> { existingMapping };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockMappingRepo.Setup(r => r.GetByTemplateIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingList);

        var useCase = new FieldMappingUseCase(_mockMappingRepo.Object, _mockTemplateRepo.Object, _mockUow.Object);

        var items = new List<SaveFieldMappingItemDto>
        {
            new("amount", "payment.amount", "จำนวนเงิน", true, "0", "thai_baht_text", 1)
        };

        // Act
        await useCase.SaveMappingsAsync(templateId, items);

        // Assert
        _mockMappingRepo.Verify(r => r.RemoveRange(existingList), Times.Once);
        _mockMappingRepo.Verify(r => r.AddAsync(It.Is<FieldMapping>(m => m.TemplateId == templateId && m.Placeholder == "amount" && m.Transform == "thai_baht_text"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
