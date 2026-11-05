using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Api.Contracts.Authoring.Templates;

/// <summary>
/// HTTP request contract for saving updated template HTML and page layout settings.
/// </summary>
public record SaveHtmlRequest(
    string Html,
    string? SamplePayload = null,
    string? ChangeNote = null,
    TemplatePageSettings? PageSettings = null
);
