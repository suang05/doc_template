using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;

public sealed class DownloadGenerationLogDocumentUseCase(
    IGenerationLogRepository logRepo,
    IStorageService storageService,
    ICurrentProjectContext projectContext) : IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult>
{
    public async Task<DownloadGenerationLogDocumentResult> ExecuteAsync(
        DownloadGenerationLogDocumentQuery query, CancellationToken ct = default)
    {
        var log = await logRepo.GetByIdAsync(query.LogId, ct)
            ?? throw new NotFoundException($"Generation log '{query.LogId}' not found.");

        if (projectContext.SystemRole != SystemRole.SuperAdmin)
        {
            if (log.ProjectId.Value != projectContext.ProjectId.Value)
            {
                throw new CrossTenantAccessDeniedException(log.ProjectId);
            }
        }

        if (string.IsNullOrWhiteSpace(log.OutputKey))
        {
            throw new NotFoundException("No output file is associated with this generation log entry.");
        }

        var stream = await storageService.DownloadAsync(StorageBuckets.Outputs, log.OutputKey, ct);
        string ext = log.OutputFormat?.Extension ?? "pdf";
        string contentType = log.OutputFormat?.MimeType ?? "application/pdf";
        string fileName = !string.IsNullOrWhiteSpace(log.OutputKey) && Path.HasExtension(log.OutputKey)
            ? Path.GetFileName(log.OutputKey)
            : $"document_{log.Id:N}.{ext}";

        return new DownloadGenerationLogDocumentResult(stream, contentType, fileName);
    }
}
