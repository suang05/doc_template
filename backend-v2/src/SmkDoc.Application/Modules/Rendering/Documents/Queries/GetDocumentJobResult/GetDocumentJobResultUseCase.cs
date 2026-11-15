using SmkDoc.Application.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJobResult;

public sealed class GetDocumentJobResultUseCase(
    IDocumentJobReadModel documentJobReadModel,
    IStorageService storageService) : IUseCase<GetDocumentJobResultQuery, DocumentJobFileResult>
{
    public async Task<DocumentJobFileResult> ExecuteAsync(GetDocumentJobResultQuery query, CancellationToken ct)
    {
        var jobId = new DocumentJobId(query.JobId);
        var artifact = await documentJobReadModel.FindArtifactAsync(jobId, ct);

        if (artifact is null)
        {
            var job = await documentJobReadModel.FindByIdAsync(jobId, ct)
                ?? throw new NotFoundException($"Document job '{query.JobId}' not found.");

            if (!string.Equals(job.Status, DocumentJobStatus.Succeeded.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleViolationException(
                    $"Document job '{query.JobId}' is in state '{job.Status}' and does not have a completed result artifact.",
                    ErrorCodes.ResourceConflict);
            }

            throw new NotFoundException($"Result artifact for document job '{query.JobId}' is unavailable.");
        }

        var stream = await storageService.DownloadAsync(StorageBuckets.Outputs, artifact.StorageKey, ct);
        return new DocumentJobFileResult(stream, artifact.ContentType, artifact.FileName);
    }
}
