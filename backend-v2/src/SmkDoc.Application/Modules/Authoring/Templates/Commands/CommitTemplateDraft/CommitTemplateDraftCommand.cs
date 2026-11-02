using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public sealed record CommitTemplateDraftCommand(
    string DraftId,
    string Name,
    string Slug,
    string? Category,
    IReadOnlyList<SaveFieldMappingItemDto> Mappings);
