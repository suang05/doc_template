using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Api.Contracts.Authoring.Templates;

/// <summary>
/// HTTP request contract for committing an uploaded template draft.
/// </summary>
public record CommitDraftRequest(
    string Name,
    string Slug,
    string? Category,
    IReadOnlyList<SaveFieldMappingItemDto> Mappings,
    Guid? ProjectId = null
);
