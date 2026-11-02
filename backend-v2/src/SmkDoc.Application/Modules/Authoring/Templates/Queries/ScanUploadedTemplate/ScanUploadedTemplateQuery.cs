namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ScanUploadedTemplate;

/// <summary>
/// Query parameters for scanning placeholders from an uploaded template stream.
/// </summary>
public sealed record ScanUploadedTemplateQuery(Stream Stream, string Extension);
