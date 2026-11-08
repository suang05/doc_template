using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.DataBinding.DTOs;

/// <summary>
/// Immutable result emitted by <see cref="Services.IDataBindingEngine"/> containing the fully-bound JSON payload
/// (with all dataset queries executed, field transforms applied, and math expressions evaluated) along with schema validation results.
/// </summary>
public sealed record BoundDocumentData(
    string DataJson,
    SchemaValidationResult? ValidationResult);

