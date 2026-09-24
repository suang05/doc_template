using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.UseCases.FieldMappings;
using SmkDoc.Domain.Entities;
using Xunit;

namespace SmkDoc.Tests;

public class FieldMappingUseCaseTests
{
    private readonly Mock<IRepository<FieldMapping>> _mockMappingRepo = new();
    private readonly Mock<IRepository<Template>> _mockTemplateRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    [Fact]
    public async Task GetMappingsByTemplateIdAsync_ShouldReturnSortedMappings()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var mappings = new List<FieldMapping>
        {
            new() { Id = Guid.NewGuid(), TemplateId = templateId, Placeholder = "total", SourcePath = "payment.total", Label = "ยอดชำระ", SortOrder = 2 },
            new() { Id = Guid.NewGuid(), TemplateId = templateId, Placeholder = "name", SourcePath = "customer.name", Label = "ชื่อลูกค้า", SortOrder = 1 }
        };

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
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
        var template = new Template { Id = templateId, Name = "Invoice", Slug = "invoice" };
        var existingMapping = new FieldMapping { Id = Guid.NewGuid(), TemplateId = templateId, Placeholder = "old", SourcePath = "old", Label = "Old" };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldMapping> { existingMapping });

        var useCase = new FieldMappingUseCase(_mockMappingRepo.Object, _mockTemplateRepo.Object, _mockUow.Object);

        var items = new List<SaveFieldMappingItem>
        {
            new("amount", "payment.amount", "จำนวนเงิน", true, "0", "thai_baht_text", 1)
        };

        // Act
        await useCase.SaveMappingsAsync(templateId, items);

        // Assert
        _mockMappingRepo.Verify(r => r.Remove(existingMapping), Times.Once);
        _mockMappingRepo.Verify(r => r.AddAsync(It.Is<FieldMapping>(m => m.TemplateId == templateId && m.Placeholder == "amount" && m.Transform == "thai_baht_text"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
