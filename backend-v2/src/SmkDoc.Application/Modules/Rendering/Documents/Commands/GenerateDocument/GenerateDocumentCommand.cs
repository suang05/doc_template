using System.Text.Json;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;

/// <summary>
/// Command for generating a document from a published template.
/// </summary>
public record GenerateDocumentCommand(
    JsonElement Data,
    string Output = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    bool SkipValidation = false
);
