using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates;

/// <summary>
/// Pre-flight template schema validation use case.
/// Validates incoming payload against published Draft-07 schema with multi-tenant scoping.
/// Pure dry-run with zero side-effects.
/// </summary>
public sealed class ValidateTemplatePayloadUseCase(
    IRepository<Template> templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IJsonSchemaValidationService schemaValidation,
    IExecutionContext executionContext)
{
    private readonly IRepository<Template> _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IJsonSchemaValidationService _schemaValidation = schemaValidation;
    private readonly IExecutionContext _executionContext = executionContext;

    public async Task<ValidateTemplatePayloadResult> ExecuteAsync(
        ValidateTemplatePayloadCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Slug))
        {
            throw new NotFoundException("Template slug cannot be empty.");
        }

        // 1. Fetch template with tenant isolation if ProjectId is bound in execution context
        var template = await _templateRepo.FirstOrDefaultAsync(
            t => t.Slug == command.Slug && t.IsActive &&
                 (!_executionContext.ProjectId.HasValue || t.ProjectId == _executionContext.ProjectId.Value),
            ct);

        if (template == null)
        {
            throw new NotFoundException($"Template '{command.Slug}' not found or inactive.");
        }

        if (template.CurrentVersionId is null)
        {
            throw new InvalidOperationException($"Template '{command.Slug}' has no published version.");
        }

        var currentVersion = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version for template '{command.Slug}' not found.");

        // 2. If no schema is stored, treat as valid pass-through
        if (string.IsNullOrWhiteSpace(currentVersion.DataSchema))
        {
            return new ValidateTemplatePayloadResult(
                Valid: true,
                TemplateSlug: command.Slug,
                Version: currentVersion.Version,
                Message: "Template has no defined schema contract (passed by default)."
            );
        }

        // 3. Evaluate payload against template Draft-07 schema
        var result = _schemaValidation.Validate(currentVersion.DataSchema, command.Data);

        if (!result.IsValid)
        {
            return new ValidateTemplatePayloadResult(
                Valid: false,
                TemplateSlug: command.Slug,
                Version: currentVersion.Version,
                Message: "Payload does not conform to the template schema.",
                Errors: result.Errors
            );
        }

        return new ValidateTemplatePayloadResult(
            Valid: true,
            TemplateSlug: command.Slug,
            Version: currentVersion.Version,
            Message: "Payload conforms to template schema."
        );
    }
}
