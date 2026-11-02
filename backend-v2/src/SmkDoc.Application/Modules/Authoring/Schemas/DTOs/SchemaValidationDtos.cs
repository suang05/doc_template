using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.Authoring.Schemas.DTOs;

/// <summary>
/// Result DTO returned by the standalone schema validation use case.
/// </summary>
public sealed record ValidateStandaloneSchemaResult(
    bool Valid,
    string Message,
    IReadOnlyList<ValidationErrorItem>? Errors = null
);
