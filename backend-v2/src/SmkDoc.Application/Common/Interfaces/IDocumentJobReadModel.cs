using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Read-only query port backed by PostgreSQL <c>document_jobs</c> and <c>document_job_items</c>
/// protected by tenant RLS and global query filters.
/// </summary>
public interface IDocumentJobReadModel
{
    Task<DocumentJobResultDto?> FindByIdAsync(DocumentJobId jobId, CancellationToken ct);
    Task<DocumentJobListResult> ListPagedAsync(string? status, string? cursor, int limit, CancellationToken ct);
    Task<IReadOnlyList<DocumentJobItemDto>> ListItemsAsync(DocumentJobId jobId, CancellationToken ct);
    Task<DocumentJobArtifactDescriptor?> FindArtifactAsync(DocumentJobId jobId, CancellationToken ct);
}
