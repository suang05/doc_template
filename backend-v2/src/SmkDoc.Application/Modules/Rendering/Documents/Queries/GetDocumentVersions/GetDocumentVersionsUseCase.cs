using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;

public sealed class GetDocumentVersionsUseCase(
    IDocumentRepository documentRepo) : IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>>
{
    public async Task<List<DocumentVersionDto>> ExecuteAsync(GetDocumentVersionsQuery query, CancellationToken ct = default)
    {
        var docRef = DocumentReference.Create(query.DocumentRef);
        var document = await documentRepo.GetByDocumentRefAsync(docRef, ct);
        if (document is null) return [];

        var versions = await documentRepo.GetVersionHistoryAsync(docRef, ct);
        return versions
            .Select(v => new DocumentVersionDto(
                v.Id, v.DocumentId.Value, document.DocumentRef,
                v.Version, v.TemplateVersionId, v.GenerationLogId,
                v.ChangeNote, v.CreatedBy, v.CreatedAt))
            .ToList();
    }
}
