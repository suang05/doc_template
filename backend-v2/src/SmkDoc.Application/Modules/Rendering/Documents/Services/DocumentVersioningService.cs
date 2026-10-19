using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentVersioningService(
    IRepository<Document> documentRepo,
    IRepository<DocumentVersion> docVersionRepo,
    IExecutionContext executionContext,
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
            document = Document.Create(
                DocumentReference.Create(documentRef),
                templateId,
                this.timeProvider.GetUtcNow(),
                executionContext.ProjectId.HasValue ? new SmkDoc.Domain.ValueObjects.Ids.ProjectId(executionContext.ProjectId.Value) : SmkDoc.Domain.ValueObjects.Ids.ProjectId.Empty);
            await documentRepo.AddAsync(document, ct);
        }

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

        await docVersionRepo.AddAsync(docVersion, ct);
    }
}
