using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Port for invalidating compiled template caches locally and broadcasting invalidations across peer pods.
/// </summary>
public interface ITemplateCacheInvalidationBus
{
    Task PublishTemplateInvalidationAsync(
        ProjectId projectId,
        TemplateId templateId,
        int version,
        CancellationToken ct = default);
}
