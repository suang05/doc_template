using System.Text.Json;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;

/// <summary>
/// Query parameters for rendering a stateless preview document.
/// </summary>
public record PreviewDocumentQuery(
    JsonElement Data,
    string? Html = null
);
