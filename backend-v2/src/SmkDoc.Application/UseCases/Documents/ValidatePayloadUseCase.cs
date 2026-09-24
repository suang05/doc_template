using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.UseCases.Documents;

/// <summary>
/// Result DTO returned by <see cref="ValidatePayloadUseCase"/>.
/// </summary>
/// <param name="IsValid">True when the payload passes all schema constraints.</param>
/// <param name="TemplateSlug">The template this validation was run against.</param>
/// <param name="SchemaVersion">The template version whose schema was used.</param>
/// <param name="Errors">Per-field error details; empty when <see cref="IsValid"/> is true.</param>
public record ValidatePayloadResult(
    bool IsValid,
    string TemplateSlug,
    int SchemaVersion,
    IReadOnlyList<SchemaValidationError> Errors);

/// <summary>
/// Pre-flight schema validation endpoint use case.
/// Validates a JSON payload against a template's stored Draft-07 schema
/// without generating any document, writing to MinIO, or touching generation_logs.
/// Zero side-effects.
/// </summary>
public sealed class ValidatePayloadUseCase
{
    private readonly IRepository<Template> _templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo;
    private readonly IJsonSchemaValidationService _schemaValidation;

    public ValidatePayloadUseCase(
        IRepository<Template> templateRepo,
        IRepository<TemplateVersion> versionRepo,
        IJsonSchemaValidationService schemaValidation)
    {
        _templateRepo     = templateRepo;
        _versionRepo      = versionRepo;
        _schemaValidation = schemaValidation;
    }

    /// <summary>
    /// Executes the pre-flight validation.
    /// </summary>
    /// <param name="slug">Template slug to validate against.</param>
    /// <param name="data">Incoming JSON payload element.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValidatePayloadResult"/> describing the outcome.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when template or its version cannot be found.</exception>
    public async Task<ValidatePayloadResult> ExecuteAsync(
        string slug, JsonElement data, CancellationToken ct = default)
    {
        var template = await _templateRepo.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (template == null || !template.IsActive)
            throw new KeyNotFoundException($"Template '{slug}' not found or inactive.");

        if (template.CurrentVersionId is null)
            throw new InvalidOperationException($"Template '{slug}' has no published version.");

        var currentVersion = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new InvalidOperationException($"Current version for template '{slug}' not found.");

        // If no schema is stored, the template has no contract — treat as valid.
        if (string.IsNullOrWhiteSpace(currentVersion.DataSchema))
        {
            return new ValidatePayloadResult(
                IsValid: true,
                TemplateSlug: slug,
                SchemaVersion: currentVersion.Version,
                Errors: []);
        }

        string dataJson = data.ValueKind != JsonValueKind.Undefined
            ? data.GetRawText()
            : "{}";

        var result = _schemaValidation.Validate(currentVersion.DataSchema, dataJson);

        return new ValidatePayloadResult(
            IsValid: result.IsValid,
            TemplateSlug: slug,
            SchemaVersion: currentVersion.Version,
            Errors: result.Errors);
    }
}
