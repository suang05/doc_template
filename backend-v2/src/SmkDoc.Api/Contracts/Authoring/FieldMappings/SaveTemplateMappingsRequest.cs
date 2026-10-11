namespace SmkDoc.Api.Contracts.Authoring.FieldMappings;

/// <summary>
/// HTTP request contract for saving (replacing) all field mappings of a template.
/// Conforms to OWASP and REST standards by wrapping collection items in a top-level object.
/// </summary>
public record SaveTemplateMappingsRequest(
    IReadOnlyList<SaveFieldMappingItemRequest> Mappings
);
