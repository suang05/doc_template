using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;

public sealed class GetDocumentJobUseCase(
    IDocumentRenderQueue renderQueue,
    ICurrentProjectContext projectContext) : IUseCase<GetDocumentJobQuery, DocumentJobDto>
{
    public async Task<DocumentJobDto> ExecuteAsync(GetDocumentJobQuery query, CancellationToken ct = default)
    {
        var job = await renderQueue.GetJobAsync(query.JobId, ct)
            ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");

        if (projectContext.SystemRole != SystemRole.SuperAdmin)
        {
            if (job.ProjectId != projectContext.ProjectId.Value)
            {
                throw new CrossTenantAccessDeniedException(job.ProjectId);
            }
        }

        return DocumentJobDto.FromJob(job);
    }
}

