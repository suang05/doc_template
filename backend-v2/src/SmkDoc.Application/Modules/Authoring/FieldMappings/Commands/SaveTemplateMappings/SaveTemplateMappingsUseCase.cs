using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;

public record SaveTemplateMappingsCommand(Guid TemplateId, List<SaveFieldMappingItemDto> Items);

public sealed class SaveTemplateMappingsUseCase(
    IFieldMappingRepository mappingRepo,
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork) : IUseCase<SaveTemplateMappingsCommand>
{
    private readonly IFieldMappingRepository _mappingRepo = mappingRepo;
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task ExecuteAsync(SaveTemplateMappingsCommand command, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var existing = await _mappingRepo.GetByTemplateIdAsync(command.TemplateId, ct);
        _mappingRepo.RemoveRange(existing);

        foreach (var item in command.Items)
        {
            var dsType = item.DataSourceType != null ? DataSourceType.FromString(item.DataSourceType) : DataSourceType.Json;
            var mapping = new FieldMapping(command.TemplateId, item.Placeholder, item.SourcePath, item.Label, item.Required, item.SortOrder, dsType);
            mapping.UpdateMappingDetails(item.SourcePath, item.Label, item.Required, item.DefaultValue, item.Transform, item.SortOrder);
            mapping.ConfigureDataSource(dsType, item.DatasetAlias, item.ResultPath, item.MathExpression);
            await _mappingRepo.AddAsync(mapping, ct);
        }

        await _unitOfWork.CommitAsync(ct);
    }
}
