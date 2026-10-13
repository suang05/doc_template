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
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task RecordVersionAsync(
        string documentRef,
        Guid templateId,
        Guid currentVersionId,
        Guid generationId,
        string? changeNote,
        CancellationToken ct = default)
    {
        var document = await documentRepo.FirstOrDefaultAsync(
            d => d.DocumentRef == documentRef, ct);

        if (document is null)
        {
            document = Document.Create(DocumentReference.Create(documentRef), templateId, this.timeProvider.GetUtcNow());
            await documentRepo.AddAsync(document, ct);
        }

        const int maxRetries = 3;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            int currentMax = await docVersionRepo.MaxOrDefaultAsync(
                v => v.DocumentId == document.Id, v => v.Version, 0, ct);

            var docVersion = DocumentVersion.Create(
                document.Id,
                currentMax + 1,
                currentVersionId,
                generationId,
                changeNote,
                executionContext.CallerApp,
                this.timeProvider.GetUtcNow());

            try
            {
                await docVersionRepo.AddAsync(docVersion, ct);
                await unitOfWork.CommitAsync(ct);
                break;
            }
            catch (Exception) when (retry < maxRetries - 1)
            {
                docVersionRepo.Remove(docVersion);
            }
        }
    }
}
