using System.Text.Json;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.DTOs.Schemas;

/// <summary>
/// Command DTO for validating an arbitrary payload against an arbitrary JSON Schema Draft-07.
/// </summary>
public record ValidateStandaloneSchemaCommand(
    JsonElement Schema,
    JsonElement Payload
);

/// <summary>
/// Result DTO returned by the standalone schema validation use case.
/// </summary>
public record ValidateStandaloneSchemaResult(
    bool Valid,
    string Message,
    IReadOnlyList<ValidationErrorItem>? Errors = null
);
