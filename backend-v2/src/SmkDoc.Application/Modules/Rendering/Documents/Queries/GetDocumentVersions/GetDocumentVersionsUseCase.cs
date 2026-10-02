using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;

public sealed class GetDocumentVersionsUseCase(
    IRepository<Document> documentRepo,
    IRepository<DocumentVersion> versionRepo) : IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>>
{
    private readonly IRepository<Document> _documentRepo = documentRepo;
    private readonly IRepository<DocumentVersion> _versionRepo = versionRepo;

    public async Task<List<DocumentVersionDto>> ExecuteAsync(GetDocumentVersionsQuery query, CancellationToken ct = default)
    {
        var document = await _documentRepo.FirstOrDefaultAsync(d => d.DocumentRef == query.DocumentRef, ct);
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
}
