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
        // Step 1: Parse & Guard
        ProjectId? projectId = null;
        if (projectContext.SystemRole != SystemRole.SuperAdmin)
        {
            projectId = projectContext.EnsureResolved("Active project context or SuperAdmin role is required to query document jobs.");
        }

        // Step 2: Ensure Preconditions
        var job = await renderQueue.GetJobAsync(query.JobId, ct)
            ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");

        if (projectId.HasValue && job.ProjectId != projectId.Value.Value)
        {
            throw new ForbiddenException("Access denied: caller does not have permission to access document jobs from another project.");
        }

        return DocumentJobDto.FromJob(job);
    }
}

