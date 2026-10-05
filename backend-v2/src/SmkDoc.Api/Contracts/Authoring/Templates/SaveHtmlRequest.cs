namespace SmkDoc.Api.Contracts.Authoring.Templates;

/// <summary>
/// HTTP request contract for saving updated template HTML.
/// </summary>
public record SaveHtmlRequest(
    string Html,
    string? SamplePayload = null,
    string? ChangeNote = null
);
