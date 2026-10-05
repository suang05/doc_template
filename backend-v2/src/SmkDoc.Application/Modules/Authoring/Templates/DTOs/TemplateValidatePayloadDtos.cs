using System.Text.Json;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

namespace SmkDoc.Application.Modules.Authoring.Templates.DTOs;

/// <summary>
/// Legacy Command alias for ValidateTemplatePayloadQuery.
/// </summary>
[Obsolete("Use SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload.ValidateTemplatePayloadQuery instead.")]
public record ValidateTemplatePayloadCommand(string Slug, JsonElement Data) 
    : ValidateTemplatePayloadQuery(Slug, Data);

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
