using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;

public sealed record SaveTemplateMappingsCommand(
    Guid TemplateId,
    IReadOnlyList<SaveFieldMappingItemDto> Items
);
