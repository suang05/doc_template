namespace SmkDoc.Api.Contracts.Authoring.Templates;

/// <summary>
/// HTTP request contract for linking datasets to a template.
/// Wraps collection items in a top-level object to conform to OWASP and REST standards.
/// </summary>
public record SaveTemplateDatasetsRequest(
    IReadOnlyList<SaveTemplateDatasetItemRequest> Datasets
);
