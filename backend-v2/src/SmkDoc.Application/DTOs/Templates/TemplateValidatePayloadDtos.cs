using System.Text.Json;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.DTOs.Templates;

/// <summary>
/// Command for validating a JSON payload against a published template's schema contract.
/// </summary>
public record ValidateTemplatePayloadCommand(
    string Slug,
    JsonElement Data
);

/// <summary>
/// Result returned after pre-flight template payload schema validation.
/// </summary>
public record ValidateTemplatePayloadResult(
    bool Valid,
    string TemplateSlug,
    int Version,
    string Message,
    IReadOnlyList<ValidationErrorItem>? Errors = null
);
