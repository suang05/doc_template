using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public record PreparedDocumentData(
    string DataJson,
    SchemaValidationResult? ValidationResult
);
