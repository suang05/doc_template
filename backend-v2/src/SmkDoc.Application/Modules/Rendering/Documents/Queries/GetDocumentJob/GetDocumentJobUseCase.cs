using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;

public sealed class GetDocumentJobUseCase(
    IDocumentRenderQueue renderQueue,
    ICurrentProjectContext projectContext) : IUseCase<GetDocumentJobQuery, DocumentJobDto>
{
    public async Task<DocumentJobDto> ExecuteAsync(GetDocumentJobQuery query, CancellationToken ct = default)
    {
        if (projectContext.SystemRole != SystemRole.SuperAdmin)
        {
            projectContext.EnsureResolved("Active project context or SuperAdmin role is required to query document jobs.");
        }

        var job = await renderQueue.GetJobAsync(query.JobId, ct)
            ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");

        if (projectContext.SystemRole != SystemRole.SuperAdmin && job.ProjectId != projectContext.ProjectId.Value)
        {
            throw new ForbiddenException("Access denied: caller does not have permission to access document jobs from another project.");
        }

        return DocumentJobDto.FromJob(job);
    }
}

