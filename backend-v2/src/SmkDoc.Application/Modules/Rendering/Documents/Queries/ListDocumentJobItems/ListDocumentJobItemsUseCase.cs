using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.ListDocumentJobItems;

public sealed class ListDocumentJobItemsUseCase(
    IDocumentJobReadModel documentJobReadModel) : IUseCase<ListDocumentJobItemsQuery, IReadOnlyList<DocumentJobItemDto>>
{
    public async Task<IReadOnlyList<DocumentJobItemDto>> ExecuteAsync(ListDocumentJobItemsQuery query, CancellationToken ct)
    {
        var jobId = new DocumentJobId(query.JobId);
        _ = await documentJobReadModel.FindByIdAsync(jobId, ct)
            ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");

        return await documentJobReadModel.ListItemsAsync(jobId, ct);
    }
}
