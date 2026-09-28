using System.Text.Json;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public record PreparedDocumentData(
    string DataJson,
    SchemaValidationResult? ValidationResult
);

public interface IDocumentDataPreparationService
{
    Task<PreparedDocumentData> PrepareDataAsync(
        Template template,
        TemplateVersion currentVersion,
        JsonElement rawData,
        bool skipValidation,
        CancellationToken ct = default);
}
