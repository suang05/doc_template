using System.Text.Json;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;

public sealed record EnqueueDocumentGenerationCommand(
    string Slug,
    JsonElement Payload,
    string OutputFormat = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    bool SkipValidation = false);
