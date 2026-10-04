using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Commands;

public class SaveTemplateMappingsUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private SaveTemplateMappingsUseCase CreateSut() =>
        new(_templateRepo.Object, _uow.Object);

    [Fact]
    public async Task ExecuteAsync_ShouldSaveMappingsAndCommit()
    {
        var templateId = Guid.NewGuid();
        var template = new Template(Guid.NewGuid(), "Contract", "contract", null, id: templateId);

        _templateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var items = new List<SaveFieldMappingItemDto>
        {
            new("customerName", "customer.name", "ชื่อลูกค้า", true, null, null, 1)
        };

        var command = new SaveTemplateMappingsCommand(templateId, items);

        await CreateSut().ExecuteAsync(command);

        template.FieldMappings.Should().HaveCount(1);
        var mapping = template.FieldMappings.First();
        mapping.TemplateId.Should().Be(templateId);
        mapping.Placeholder.Should().Be("customerName");
        mapping.SourcePath.Should().Be("customer.name");
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        var templateId = Guid.NewGuid();
        _templateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var command = new SaveTemplateMappingsCommand(templateId, new List<SaveFieldMappingItemDto>());

        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
