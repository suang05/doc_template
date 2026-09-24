using System.Text.Json;

namespace SmkDoc.Application.Common.Models;

public record GenerateDocumentRequest(
    JsonElement Data,
    string Output = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    /// <summary>
    /// When true, the Draft-07 schema validation gate is skipped entirely.
    /// Use only for backward-compatible callers that have not yet adopted the contract.
    /// </summary>
    bool SkipValidation = false
);
