using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

/// <summary>
/// Prepares and validates document data for rendering without mutating input fields.
/// Invariant: Zero field mutation. Payload flows untouched to JSON schema validator and rendering engine.
/// </summary>
public sealed class DocumentDataPreparationService(
    IJsonSchemaValidationService schemaValidation) : IDocumentDataPreparationService
{
    public Task<PreparedDocumentData> PrepareDataAsync(
        TemplateVersion currentVersion,
        JsonElement rawData,
        bool skipValidation,
        CancellationToken ct = default)
    {
        // 1. Raw Payload Invariant: Do NOT mutate, map, or alter fields.
        string dataJson = rawData.ValueKind != JsonValueKind.Undefined
            ? rawData.GetRawText()
            : "{}";

        // 2. Fail-Fast Draft-07 Schema Validation Gate
        SchemaValidationResult? validationResult = null;
        if (!skipValidation && !string.IsNullOrWhiteSpace(currentVersion.DataSchema))
        {
            validationResult = schemaValidation.Validate(currentVersion.DataSchema, dataJson);
        }

        return Task.FromResult(new PreparedDocumentData(dataJson, validationResult));
    }
}
