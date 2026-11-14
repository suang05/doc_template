using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmkDoc.Api.Contracts.Rendering.Documents;

/// <summary>
/// HTTP request contract for ephemeral document preview.
/// </summary>
public record PreviewDocumentRequest(
    [property: JsonRequired] JsonElement Payload,
    string? Html = null
);
