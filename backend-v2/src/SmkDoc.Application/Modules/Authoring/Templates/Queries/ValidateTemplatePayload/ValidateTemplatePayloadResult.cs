using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

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
