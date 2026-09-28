using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents;

/// <summary>
/// Pre-flight schema validation endpoint use case.
/// Validates a JSON payload against a template's stored Draft-07 schema
/// without generating any document, writing to MinIO, or touching generation_logs.
/// Zero side-effects.
/// </summary>
public sealed class ValidatePayloadUseCase(
    IRepository<Template> templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IJsonSchemaValidationService schemaValidation)
{
    private readonly IRepository<Template> _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IJsonSchemaValidationService _schemaValidation = schemaValidation;

    /// <summary>
    /// Executes the pre-flight validation.
    /// </summary>
    /// <param name="slug">Template slug to validate against.</param>
    /// <param name="data">Incoming JSON payload element.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValidatePayloadResult"/> describing the outcome.</returns>
    /// <exception cref="NotFoundException">Thrown when template or its version cannot be found.</exception>
    public async Task<ValidatePayloadResult> ExecuteAsync(
        string slug, JsonElement data, CancellationToken ct = default)
    {
        var template = await _templateRepo.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (template == null || !template.IsActive)
            throw new NotFoundException($"Template '{slug}' not found or inactive.");

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
