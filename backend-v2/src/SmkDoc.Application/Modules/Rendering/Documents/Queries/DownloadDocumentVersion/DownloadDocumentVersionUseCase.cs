using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;

public sealed class DownloadDocumentVersionUseCase(
    IDocumentRepository documentRepo,
    IGenerationLogRepository logRepo,
    IStorageService storageService) : IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult>
{
    public async Task<DownloadDocumentVersionResult> ExecuteAsync(DownloadDocumentVersionQuery query, CancellationToken ct = default)
    {
        var docRef = DocumentReference.Create(query.DocumentRef);
        var document = await documentRepo.GetByDocumentRefAsync(docRef, ct)
            ?? throw new NotFoundException($"Document '{query.DocumentRef}' not found.");

        var docVersion = await documentRepo.GetVersionAsync(docRef, query.Version, ct)
            ?? throw new NotFoundException($"Version {query.Version} not found for document '{query.DocumentRef}'.");

        if (docVersion.GenerationLogId is null)
            throw new NotFoundException($"Document version {query.Version} has no linked generation log.");

        var log = await logRepo.GetByIdAsync(docVersion.GenerationLogId.Value, ct)
            ?? throw new NotFoundException($"Generation log for document version {query.Version} not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new NotFoundException("No output file is associated with this document version.");

        var stream = await storageService.DownloadAsync(StorageBuckets.Outputs, log.OutputKey, ct);
        string ext = log.OutputFormat?.Extension ?? "pdf";
        string contentType = log.OutputFormat?.MimeType ?? "application/pdf";

        return new DownloadDocumentVersionResult(stream, contentType, $"{query.DocumentRef}_v{query.Version}.{ext}");
    }
}
