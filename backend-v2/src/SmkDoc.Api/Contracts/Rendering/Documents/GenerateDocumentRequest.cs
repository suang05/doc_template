using System.Text.Json;

namespace SmkDoc.Api.Contracts.Rendering.Documents;

/// <summary>
/// HTTP request contract for document generation.
/// </summary>
public record GenerateDocumentRequest(
    JsonElement Data,
    string Output = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    bool SkipValidation = false
);
