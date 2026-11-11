using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentVersioningService(
    IDocumentRepository documentRepo,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IDocumentVersioningService
{
    public async Task RecordVersionAsync(
        string documentRef,
        Guid templateId,
        Guid currentVersionId,
        Guid generationId,
        string? changeNote,
        CancellationToken ct = default)
    {
        var docRef = DocumentReference.Create(documentRef);
        var document = await documentRepo.GetByDocumentRefWithVersionsAsync(docRef, ct);

        var now = timeProvider.GetUtcNow();

        if (document is null)
        {
            var projectId = projectContext.EnsureResolved(
                "Document versioning requires a valid project context.");

            document = Document.Create(
                docRef,
                new TemplateId(templateId),
                now,
                projectId);
            await documentRepo.AddAsync(document, ct);
        }

        var createdBy = projectContext.CallerIdentity?.Trim();
        if (createdBy is { Length: > DocumentVersion.MaxCreatedByLength })
        {
            createdBy = createdBy[..DocumentVersion.MaxCreatedByLength];
        }

        document.CreateVersion(
            currentVersionId,
            generationId,
            changeNote,
            createdBy,
            now);
    }
}
