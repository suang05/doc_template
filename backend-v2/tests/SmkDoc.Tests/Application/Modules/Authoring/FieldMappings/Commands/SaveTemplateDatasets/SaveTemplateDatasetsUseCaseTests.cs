using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public sealed class SaveTemplateDatasetsUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private SaveTemplateDatasetsUseCase CreateSut() =>
        new(_templateRepo, _unitOfWork, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ReplacesDatasetsAndCommits()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("Receipt")
            .WithSlug("receipt")
            .Build();
        template.AttachDataset(DatasetId.New(), DatasetAlias.Create("old_alias"), 1, TestConstants.BaselineTime);

        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);

        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        var dsId = Guid.NewGuid();
        var items = new List<SaveTemplateDatasetItemDto>
        {
            new(dsId, "payments", 1)
        };

        var command = new SaveTemplateDatasetsCommand(templateId, items);

        // Act
        await CreateSut().ExecuteAsync(command);

        // Assert
        template.TemplateDatasets.Should().HaveCount(1);
        template.TemplateDatasets.First().Alias.Value.Should().Be("payments");
        template.TemplateDatasets.First().DatasetId.Value.Should().Be(dsId);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns((Template?)null);
        _templateRepo.GetByIdAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        var command = new SaveTemplateDatasetsCommand(templateId, []);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{templateId}*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDuplicateAliasesProvided_ThrowsBusinessRuleViolationException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("Contract")
            .WithSlug("contract")
            .Build();

        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);

        var items = new List<SaveTemplateDatasetItemDto>
        {
            new(Guid.NewGuid(), "orders", 1),
            new(Guid.NewGuid(), "ORDERS ", 2) // Duplicate case-insensitive
        };

        var command = new SaveTemplateDatasetsCommand(templateId, items);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleViolationException>();
        ex.Which.ErrorCode.Should().Be("DUPLICATE_DATASET_ALIAS");
    }
}
