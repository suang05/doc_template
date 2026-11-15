using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJobStatus;

public sealed class GetDocumentJobStatusUseCase(
    IDocumentJobReadModel documentJobReadModel) : IUseCase<GetDocumentJobStatusQuery, DocumentJobResultDto>
{
    public async Task<DocumentJobResultDto> ExecuteAsync(GetDocumentJobStatusQuery query, CancellationToken ct) =>
        await documentJobReadModel.FindByIdAsync(new DocumentJobId(query.JobId), ct)
        ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");
}
