using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public record SaveTemplateDatasetsCommand(Guid TemplateId, List<SaveTemplateDatasetItemDto> Items);

public sealed class SaveTemplateDatasetsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork) : IUseCase<SaveTemplateDatasetsCommand>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task ExecuteAsync(SaveTemplateDatasetsCommand command, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdWithDetailsAsync(command.TemplateId, ct)
            ?? await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var datasets = command.Items.Select(item =>
            new TemplateDataset(command.TemplateId, item.DatasetId, item.Alias.Trim().ToLowerInvariant(), item.SortOrder)
        ).ToList();

        template.ReplaceDatasets(datasets);
        await _unitOfWork.CommitAsync(ct);
    }
}
