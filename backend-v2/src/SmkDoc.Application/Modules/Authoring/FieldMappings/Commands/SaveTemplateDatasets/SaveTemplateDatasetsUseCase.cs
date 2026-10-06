using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public record SaveTemplateDatasetsCommand(Guid TemplateId, List<SaveTemplateDatasetItemDto> Items);

public sealed class SaveTemplateDatasetsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IUseCase<SaveTemplateDatasetsCommand>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task ExecuteAsync(SaveTemplateDatasetsCommand command, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdWithDetailsAsync(command.TemplateId, ct)
            ?? await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var now = _timeProvider.GetUtcNow();
        var datasets = command.Items.Select(item =>
            TemplateDataset.Create(command.TemplateId, item.DatasetId, DatasetAlias.Create(item.Alias), item.SortOrder, now)
        ).ToList();

        template.ReplaceDatasets(datasets, now);
        await _unitOfWork.CommitAsync(ct);
    }
}
