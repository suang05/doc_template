using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

/// <summary>
/// Pre-flight template schema validation use case.
/// Validates incoming payload against published Draft-07 schema with multi-tenant scoping.
/// Pure dry-run with zero side-effects.
/// </summary>
public class ValidateTemplatePayloadUseCase(
    ITemplateRepository templateRepo,
    IJsonSchemaValidationService schemaValidation) : IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult>
{
    public async Task<ValidateTemplatePayloadResult> ExecuteAsync(
        ValidateTemplatePayloadQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Slug))
        {
            throw new NotFoundException("Template slug cannot be empty.");
        }

        // 1. Fetch template with tenant isolation via Global Query Filter
        var template = await templateRepo.GetBySlugAsync(query.Slug, ct);

        if (template == null || !template.IsActive)
        {
            throw new NotFoundException($"Template '{query.Slug}' not found or inactive.");
        }

        if (template.CurrentVersionId is null)
        {
            throw new BusinessRuleViolationException($"Template '{query.Slug}' has no published version.");
        }

        var currentVersion = template.CurrentVersion
            ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version for template '{query.Slug}' not found.");

        // 2. If no schema is stored, treat as valid pass-through
        if (string.IsNullOrWhiteSpace(currentVersion.DataSchema))
        {
            return new ValidateTemplatePayloadResult(
                Valid: true,
                TemplateSlug: query.Slug,
                Version: currentVersion.Version,
                Message: "Template has no defined schema contract (passed by default)."
            );
        }

        // 3. Evaluate payload against template Draft-07 schema
        var result = schemaValidation.Validate(currentVersion.DataSchema, query.Data);

        if (!result.IsValid)
        {
            return new ValidateTemplatePayloadResult(
                Valid: false,
                TemplateSlug: query.Slug,
                Version: currentVersion.Version,
                Message: "Payload does not conform to the template schema.",
                Errors: result.Errors
            );
        }

        return new ValidateTemplatePayloadResult(
            Valid: true,
            TemplateSlug: query.Slug,
            Version: currentVersion.Version,
            Message: "Payload conforms to template schema."
        );
    }
}
