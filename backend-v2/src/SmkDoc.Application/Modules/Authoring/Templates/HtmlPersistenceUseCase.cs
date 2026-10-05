using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates;

/// <summary>
/// Legacy alias for SaveTemplateHtmlUseCase. Retained for backwards compatibility.
/// </summary>
[Obsolete("Use SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml.SaveTemplateHtmlUseCase instead.")]
public class HtmlPersistenceUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    IExecutionContext executionContext,
    ISchemaInferenceService schemaInferenceService) : Commands.SaveTemplateHtml.SaveTemplateHtmlUseCase(
        templateRepo, versionRepo, storageService, unitOfWork, executionContext, schemaInferenceService)
{
}
