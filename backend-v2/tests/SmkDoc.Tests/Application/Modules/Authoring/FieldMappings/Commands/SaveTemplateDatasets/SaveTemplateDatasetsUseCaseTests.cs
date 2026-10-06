using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public class SaveTemplateDatasetsUseCaseTests
{
    private readonly Mock<ITemplateRepository> _templateRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private SaveTemplateDatasetsUseCase CreateSut() =>
        new(_templateRepoMock.Object, _unitOfWorkMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ReplacesDatasetsAndCommits()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "Receipt", "receipt");
        template.AttachDataset(Guid.NewGuid(), DatasetAlias.Create("old_alias"), 1, TestConstants.BaselineTime);

        _templateRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _unitOfWorkMock
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dsId = Guid.NewGuid();
        var items = new List<SaveTemplateDatasetItemDto>
        {
            new(dsId, "payments", 1)
        };

        var command = new SaveTemplateDatasetsCommand(templateId, items);
        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync(command);

        // Assert
        template.TemplateDatasets.Should().HaveCount(1);
        template.TemplateDatasets.First().Alias.Value.Should().Be("payments");
        template.TemplateDatasets.First().DatasetId.Should().Be(dsId);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);
        _templateRepoMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var command = new SaveTemplateDatasetsCommand(templateId, new List<SaveTemplateDatasetItemDto>());
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{templateId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDuplicateAliasesProvided_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TemplateTestFactory.Create(templateId, Guid.NewGuid(), "Contract", "contract"));

        var items = new List<SaveTemplateDatasetItemDto>
        {
            new(Guid.NewGuid(), "orders", 1),
            new(Guid.NewGuid(), "ORDERS ", 2) // Duplicate case-insensitive
        };

        var command = new SaveTemplateDatasetsCommand(templateId, items);
        var sut = CreateSut();

        // Act
        var act = () => sut.ExecuteAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
    }
}
