namespace SmkDoc.Api.Contracts.Authoring.Templates;

/// <summary>
/// HTTP request contract for previewing a draft template with sample JSON payload.
/// </summary>
public record PreviewDraftRequest(string DataJson);
