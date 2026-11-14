using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;

public sealed class GetDocumentJobUseCase(
    IDocumentRenderQueue renderQueue,
    ICurrentProjectContext projectContext) : IUseCase<GetDocumentJobQuery, DocumentJobDto>
{
    public async Task<DocumentJobDto> ExecuteAsync(GetDocumentJobQuery query, CancellationToken ct)
    {
        var job = await renderQueue.GetJobAsync(query.JobId, ct)
            ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");

        if (projectContext.SystemRole != SystemRole.SuperAdmin &&
            job.ProjectId != projectContext.GetRequiredProjectId().Value)
        {
            throw new CrossTenantAccessDeniedException(job.ProjectId);
        }

        return DocumentJobDto.FromJob(job);
    }
}

