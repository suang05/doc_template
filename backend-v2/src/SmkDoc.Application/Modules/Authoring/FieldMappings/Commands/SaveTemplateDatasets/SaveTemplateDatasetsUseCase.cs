using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public record SaveTemplateDatasetsCommand(Guid TemplateId, List<SaveTemplateDatasetItemDto> Items);

public sealed class SaveTemplateDatasetsUseCase(
    ITemplateDatasetRepository tdRepo,
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork) : IUseCase<SaveTemplateDatasetsCommand>
{
    private readonly ITemplateDatasetRepository _tdRepo = tdRepo;
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task ExecuteAsync(SaveTemplateDatasetsCommand command, CancellationToken ct = default)
    {
        _ = await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        // Validate unique aliases
        var aliases = command.Items.Select(i => i.Alias.Trim().ToLowerInvariant()).ToList();
        if (aliases.Count != aliases.Distinct().Count())
            throw new InvalidOperationException("Dataset aliases must be unique within a template.");

        // Replace all existing assignments
        var existing = await _tdRepo.GetByTemplateIdAsync(command.TemplateId, ct);
        _tdRepo.RemoveRange(existing);

        foreach (var item in command.Items)
        {
            await _tdRepo.AddAsync(new TemplateDataset(command.TemplateId, item.DatasetId, item.Alias.Trim().ToLowerInvariant(), item.SortOrder), ct);
        }

        await _unitOfWork.CommitAsync(ct);
    }
}
