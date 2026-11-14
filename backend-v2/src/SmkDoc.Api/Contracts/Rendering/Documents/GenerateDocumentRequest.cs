using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmkDoc.Api.Contracts.Rendering.Documents;

/// <summary>
/// HTTP request contract for document generation.
/// </summary>
public record GenerateDocumentRequest(
    [property: JsonRequired] JsonElement Payload,
    string OutputFormat = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    bool? SkipValidation = null
);
