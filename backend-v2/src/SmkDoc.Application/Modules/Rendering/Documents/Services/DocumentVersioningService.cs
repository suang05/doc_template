using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentVersioningService(
    IRepository<Document> documentRepo,
    IRepository<DocumentVersion> docVersionRepo,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IDocumentVersioningService
{
    private readonly IRepository<Document> _documentRepo = documentRepo;
    private readonly IRepository<DocumentVersion> _docVersionRepo = docVersionRepo;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task RecordVersionAsync(
        string documentRef,
        Guid templateId,
        Guid currentVersionId,
        Guid generationId,
        string? changeNote,
        CancellationToken ct = default)
    {
        var document = await _documentRepo.FirstOrDefaultAsync(
            d => d.DocumentRef == documentRef, ct);

        if (document is null)
        {
            document = Document.Create(DocumentReference.Create(documentRef), templateId, _timeProvider.GetUtcNow());
            await _documentRepo.AddAsync(document, ct);
        }

        const int maxRetries = 3;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            int currentMax = await _docVersionRepo.MaxOrDefaultAsync(
                v => v.DocumentId == document.Id, v => v.Version, 0, ct);

            var docVersion = DocumentVersion.Create(
                document.Id,
                currentMax + 1,
                currentVersionId,
                generationId,
                changeNote,
                _executionContext.CallerApp,
                _timeProvider.GetUtcNow());

            try
            {
                await _docVersionRepo.AddAsync(docVersion, ct);
                await _unitOfWork.CommitAsync(ct);
                break;
            }
            catch (Exception) when (retry < maxRetries - 1)
            {
                _docVersionRepo.Remove(docVersion);
            }
        }
    }
}
