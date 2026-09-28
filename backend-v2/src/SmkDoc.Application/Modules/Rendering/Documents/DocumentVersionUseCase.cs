using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Rendering.Documents;

public sealed class DocumentVersionUseCase(
    IRepository<Document> documentRepo,
    IRepository<DocumentVersion> versionRepo,
    IRepository<GenerationLog> logRepo,
    IStorageService storageService)
{
    public async Task<List<DocumentVersionDto>> GetVersionsByRefAsync(string documentRef, CancellationToken ct = default)
    {
        var document = await documentRepo.FirstOrDefaultAsync(d => d.DocumentRef == documentRef, ct);
        if (document is null) return [];

        var versions = await versionRepo.ListAsync(v => v.DocumentId == document.Id, ct);
        return versions
            .OrderByDescending(v => v.Version)
            .Select(v => new DocumentVersionDto(
                v.Id, v.DocumentId, document.DocumentRef,
                v.Version, v.TemplateVersionId, v.GenerationLogId,
                v.ChangeNote, v.CreatedBy, v.CreatedAt))
            .ToList();
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadVersionAsync(
        string documentRef, int version, CancellationToken ct = default)
    {
        var document = await documentRepo.FirstOrDefaultAsync(d => d.DocumentRef == documentRef, ct)
            ?? throw new NotFoundException($"Document '{documentRef}' not found.");

        var docVersion = await versionRepo.FirstOrDefaultAsync(
            v => v.DocumentId == document.Id && v.Version == version, ct)
            ?? throw new NotFoundException($"Version {version} not found for document '{documentRef}'.");

        if (docVersion.GenerationLogId is null)
            throw new InvalidOperationException($"Document version {version} has no linked generation log.");

        var log = await logRepo.GetByIdAsync(docVersion.GenerationLogId.Value, ct)
            ?? throw new NotFoundException($"Generation log for document version {version} not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new InvalidOperationException("No output file is associated with this document version.");

        var stream = await storageService.DownloadAsync(StorageBuckets.Outputs, log.OutputKey, ct);
        string ext = log.OutputFormat?.Extension ?? "pdf";
        string contentType = log.OutputFormat?.MimeType ?? "application/pdf";
        return (stream, contentType, $"{documentRef}_v{version}.{ext}");
    }

    public async Task<string> GetDownloadUrlByLogIdAsync(Guid logId, CancellationToken ct = default)
    {
        var log = await logRepo.GetByIdAsync(logId, ct)
            ?? throw new NotFoundException($"Generation log '{logId}' not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new InvalidOperationException("No output file is associated with this log entry.");

        return await storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, log.OutputKey, TimeSpan.FromHours(1), ct);
    }
}
