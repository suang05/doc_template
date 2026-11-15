using SmkDoc.Application.Modules.Rendering.Documents.DTOs;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobs;

public sealed class ListDocumentJobsUseCase(
    IDocumentJobReadModel documentJobReadModel) : IUseCase<ListDocumentJobsQuery, DocumentJobListResult>
{
    public Task<DocumentJobListResult> ExecuteAsync(ListDocumentJobsQuery query, CancellationToken ct) =>
        documentJobReadModel.ListPagedAsync(query.Status, query.Cursor, Math.Clamp(query.Limit, 1, 100), ct);
}
