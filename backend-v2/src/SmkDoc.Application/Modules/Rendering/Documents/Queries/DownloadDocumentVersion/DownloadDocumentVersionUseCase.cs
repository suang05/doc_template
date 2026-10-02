using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;

public sealed class DownloadDocumentVersionUseCase(
    IRepository<Document> documentRepo,
    IRepository<DocumentVersion> versionRepo,
    IRepository<GenerationLog> logRepo,
    IStorageService storageService) : IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult>
{
    private readonly IRepository<Document> _documentRepo = documentRepo;
    private readonly IRepository<DocumentVersion> _versionRepo = versionRepo;
    private readonly IRepository<GenerationLog> _logRepo = logRepo;
    private readonly IStorageService _storageService = storageService;

    public async Task<DownloadDocumentVersionResult> ExecuteAsync(DownloadDocumentVersionQuery query, CancellationToken ct = default)
    {
        var document = await _documentRepo.FirstOrDefaultAsync(d => d.DocumentRef == query.DocumentRef, ct)
            ?? throw new NotFoundException($"Document '{query.DocumentRef}' not found.");

        var docVersion = await _versionRepo.FirstOrDefaultAsync(
            v => v.DocumentId == document.Id && v.Version == query.Version, ct)
            ?? throw new NotFoundException($"Version {query.Version} not found for document '{query.DocumentRef}'.");

        if (docVersion.GenerationLogId is null)
            throw new InvalidOperationException($"Document version {query.Version} has no linked generation log.");

        var log = await _logRepo.GetByIdAsync(docVersion.GenerationLogId.Value, ct)
            ?? throw new NotFoundException($"Generation log for document version {query.Version} not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new InvalidOperationException("No output file is associated with this document version.");

        var stream = await _storageService.DownloadAsync(StorageBuckets.Outputs, log.OutputKey, ct);
        string ext = log.OutputFormat?.Extension ?? "pdf";
        string contentType = log.OutputFormat?.MimeType ?? "application/pdf";

        return new DownloadDocumentVersionResult(stream, contentType, $"{query.DocumentRef}_v{query.Version}.{ext}");
    }
}
