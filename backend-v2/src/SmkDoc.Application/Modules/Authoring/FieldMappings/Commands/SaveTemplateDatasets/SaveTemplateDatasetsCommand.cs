using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public sealed record SaveTemplateDatasetsCommand(
    Guid TemplateId,
    List<SaveTemplateDatasetItemDto> Items,
    string? IfMatch = null
);
