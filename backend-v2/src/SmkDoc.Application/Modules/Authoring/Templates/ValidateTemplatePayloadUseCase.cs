using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates;

/// <summary>
/// Legacy wrapper for ValidateTemplatePayloadUseCase. Retained for backwards compatibility.
/// </summary>
[Obsolete("Use SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload.ValidateTemplatePayloadUseCase instead.")]
public class ValidateTemplatePayloadUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IJsonSchemaValidationService schemaValidation,
    IExecutionContext executionContext)
    : Queries.ValidateTemplatePayload.ValidateTemplatePayloadUseCase(templateRepo, versionRepo, schemaValidation, executionContext)
{
}
