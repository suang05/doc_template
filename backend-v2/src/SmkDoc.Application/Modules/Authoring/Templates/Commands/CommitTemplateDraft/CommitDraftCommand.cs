using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

/// <summary>
/// Command payload for committing a draft template to persistent storage and database.
/// </summary>
public record CommitDraftCommand(
    string Name,
    string Slug,
    string? Category,
    IReadOnlyList<SaveFieldMappingItemDto> Mappings,
    Guid? ProjectId = null
);
