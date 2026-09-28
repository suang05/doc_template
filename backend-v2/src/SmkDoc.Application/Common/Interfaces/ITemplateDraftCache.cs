using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Common.Interfaces;

public interface ITemplateDraftCache
{
    Task<string> StoreAsync(TemplateDraftEntry draft, CancellationToken ct = default);
    Task<TemplateDraftEntry?> GetAsync(string draftId, CancellationToken ct = default);
    Task RemoveAsync(string draftId, CancellationToken ct = default);
}
