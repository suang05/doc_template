using System.Text.Json;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings;

/// <summary>
/// Legacy wrapper for PreviewMappingUseCase. Retained for backwards compatibility.
/// </summary>
[Obsolete("Use SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping.PreviewMappingUseCase instead.")]
public class PreviewMappingUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IStorageService storageService,
    IEnumerable<IRenderEngine> engines,
    IFieldMappingApplicatorService fieldMappingApplicator,
    IDataProtectionService dataProtection)
    : Queries.PreviewMapping.PreviewMappingUseCase(
        templateRepo, versionRepo, datasetRepo, connectionRepo, storageService, engines, fieldMappingApplicator, dataProtection)
{
}
