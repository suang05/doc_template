using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentVersioningService(
    IRepository<Document> documentRepo,
    IRepository<DocumentVersion> docVersionRepo,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork) : IDocumentVersioningService
{
    private readonly IRepository<Document> _documentRepo = documentRepo;
    private readonly IRepository<DocumentVersion> _docVersionRepo = docVersionRepo;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

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
            document = new Document(documentRef, templateId);
            await _documentRepo.AddAsync(document, ct);
        }

        const int maxRetries = 3;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            int currentMax = await _docVersionRepo.MaxOrDefaultAsync(
                v => v.DocumentId == document.Id, v => v.Version, 0, ct);

            var docVersion = new DocumentVersion(
                document.Id,
                currentMax + 1,
                currentVersionId,
                generationId,
                changeNote,
                _executionContext.CallerApp);

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
