using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentVersioningService(
    IDocumentRepository documentRepo,
    IExecutionContext executionContext,
    ICurrentProjectContext projectContext,
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
        var docRef = DocumentReference.Create(documentRef);
        var document = await documentRepo.GetByDocumentRefWithVersionsAsync(docRef, ct);

        var now = this.timeProvider.GetUtcNow();

        if (document is null)
        {
            var projectId = projectContext.IsResolved
                ? projectContext.ProjectId
                : throw new SmkDoc.Domain.Exceptions.BusinessRuleViolationException(
                    "Document versioning requires a valid project context.", "MISSING_PROJECT_CONTEXT");

            document = Document.Create(
                docRef,
                new TemplateId(templateId),
                now,
                projectId);
            await documentRepo.AddAsync(document, ct);
        }

        document.CreateVersion(
            currentVersionId,
            generationId,
            changeNote,
            executionContext.CallerApp,
            now);
    }
}
