using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;

public class SaveTemplateMappingsUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private SaveTemplateMappingsUseCase CreateSut() =>
        new(_templateRepoMock.Object, _uowMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_SavesMappingsAndCommits()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "Contract", "contract");

        _templateRepoMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var items = new List<SaveFieldMappingItemDto>
        {
            new("customerName", "customer.name", "ชื่อลูกค้า", true, null, "thai_baht_text", 1)
        };

        var command = new SaveTemplateMappingsCommand(templateId, items);
        // Act
        await CreateSut().ExecuteAsync(command);

        // Assert
        template.FieldMappings.Should().HaveCount(1);
        var mapping = template.FieldMappings.First();
        mapping.TemplateId.Should().Be(templateId);
        mapping.Placeholder.Should().Be("customerName");
        mapping.SourcePath.Should().Be("customer.name");
        mapping.Transform.Should().Be("thai_baht_text");
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepoMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var command = new SaveTemplateMappingsCommand(templateId, new List<SaveFieldMappingItemDto>());

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
