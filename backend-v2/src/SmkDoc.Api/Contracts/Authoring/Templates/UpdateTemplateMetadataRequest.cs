namespace SmkDoc.Api.Contracts.Authoring.Templates;

/// <summary>
/// HTTP request contract for updating template metadata.
/// </summary>
public record UpdateTemplateMetadataRequest(
    string? Name,
    string? Category,
    bool? IsActive = null
);
