using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Tenant-isolated, zero-LOH draft template cache storing metadata in Redis/Memory and binary streams in StorageBuckets.Drafts.
/// </summary>
public interface ITemplateDraftCache
{
    Task<string> StoreAsync(
        ProjectId projectId,
        Stream contentStream,
        string fileName,
        string fileExtension,
        IReadOnlyList<string> placeholders,
        CancellationToken ct);

    Task<TemplateDraftEntry?> GetAsync(
        ProjectId projectId,
        string draftId,
        CancellationToken ct);

    Task<TemplateDraftEntry?> GetCurrentProjectDraftAsync(
        string draftId,
        CancellationToken ct);

    Task<Stream> OpenStreamAsync(
        TemplateDraftEntry draft,
        CancellationToken ct);

    Task RemoveAsync(
        ProjectId projectId,
        string draftId,
        CancellationToken ct);
}
