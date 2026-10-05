using System.Text.Json;

namespace SmkDoc.Api.Contracts.Rendering.Documents;

/// <summary>
/// HTTP request contract for ephemeral document preview.
/// </summary>
public record PreviewDocumentRequest(
    JsonElement Data,
    string? Html = null
);
