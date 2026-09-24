using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.UseCases.Documents;

public class DocumentVersionUseCase
{
    private readonly IRepository<Document> _documentRepo;
    private readonly IRepository<DocumentVersion> _versionRepo;
    private readonly IRepository<GenerationLog> _logRepo;
    private readonly IStorageService _storageService;

    public DocumentVersionUseCase(
        IRepository<Document> documentRepo,
        IRepository<DocumentVersion> versionRepo,
        IRepository<GenerationLog> logRepo,
        IStorageService storageService)
    {
        _documentRepo = documentRepo;
        _versionRepo = versionRepo;
        _logRepo = logRepo;
        _storageService = storageService;
    }

    public async Task<List<DocumentVersionDto>> GetVersionsByRefAsync(string documentRef, CancellationToken ct = default)
    {
        var document = await _documentRepo.FirstOrDefaultAsync(d => d.DocumentRef == documentRef, ct);
        if (document is null) return [];

        var versions = await _versionRepo.ListAsync(v => v.DocumentId == document.Id, ct);
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
        var document = await _documentRepo.FirstOrDefaultAsync(d => d.DocumentRef == documentRef, ct)
            ?? throw new KeyNotFoundException($"Document '{documentRef}' not found.");

        var docVersion = await _versionRepo.FirstOrDefaultAsync(
            v => v.DocumentId == document.Id && v.Version == version, ct)
            ?? throw new KeyNotFoundException($"Version {version} not found for document '{documentRef}'.");

        if (docVersion.GenerationLogId is null)
            throw new InvalidOperationException($"Document version {version} has no linked generation log.");

        var log = await _logRepo.GetByIdAsync(docVersion.GenerationLogId.Value, ct)
            ?? throw new KeyNotFoundException($"Generation log for document version {version} not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new InvalidOperationException("No output file is associated with this document version.");

        var stream = await _storageService.DownloadAsync(StorageBuckets.Outputs, log.OutputKey, ct);
        string ext = (log.OutputFormat ?? "pdf").ToLowerInvariant();
        string contentType = ext switch
        {
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _      => "application/pdf"
        };
        return (stream, contentType, $"{documentRef}_v{version}.{ext}");
    }

    public async Task<string> GetDownloadUrlByLogIdAsync(Guid logId, CancellationToken ct = default)
    {
        var log = await _logRepo.GetByIdAsync(logId, ct)
            ?? throw new KeyNotFoundException($"Generation log '{logId}' not found.");

        if (string.IsNullOrWhiteSpace(log.OutputKey))
            throw new InvalidOperationException("No output file is associated with this log entry.");

        return await _storageService.GetPresignedUrlAsync(StorageBuckets.Outputs, log.OutputKey, TimeSpan.FromHours(1), ct);
    }
}
