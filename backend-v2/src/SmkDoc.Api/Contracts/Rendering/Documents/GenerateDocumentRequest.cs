using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmkDoc.Api.Contracts.Rendering.Documents;

/// <summary>
/// HTTP request contract for synchronous document creation (<c>POST /api/v1/documents</c>).
/// </summary>
public record CreateDocumentRequest(
    string Slug,
    [property: JsonRequired] JsonElement Payload,
    string OutputFormat = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    bool? SkipValidation = null
);

/// <summary>
/// HTTP request contract for legacy slug-scoped document generation.
/// </summary>
public record GenerateDocumentRequest(
    [property: JsonRequired] JsonElement Payload,
    string OutputFormat = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    bool? SkipValidation = null
);
