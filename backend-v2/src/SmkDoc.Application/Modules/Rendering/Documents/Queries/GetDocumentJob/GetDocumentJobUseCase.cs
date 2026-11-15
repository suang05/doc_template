using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;

public sealed class GetDocumentJobUseCase(
    IDocumentJobReadModel documentJobReadModel) : IUseCase<GetDocumentJobQuery, DocumentJobDto>
{
    public async Task<DocumentJobDto> ExecuteAsync(GetDocumentJobQuery query, CancellationToken ct)
    {
        var job = await documentJobReadModel.FindByIdAsync(new DocumentJobId(query.JobId), ct)
            ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");

        return DocumentJobDto.FromReadModel(job);
    }
}
