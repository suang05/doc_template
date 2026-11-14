using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Document"/> aggregate roots.
/// </summary>
public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken ct);
    Task<Document?> GetByDocumentRefAsync(DocumentReference documentRef, CancellationToken ct);
    Task<Document?> GetByDocumentRefWithVersionsAsync(DocumentReference documentRef, CancellationToken ct);
    Task<DocumentVersion?> GetVersionAsync(DocumentReference documentRef, int version, CancellationToken ct);
    Task<IReadOnlyList<DocumentVersion>> GetVersionHistoryAsync(DocumentReference documentRef, CancellationToken ct);
    Task<int> GetNextVersionNumberAsync(DocumentId documentId, CancellationToken ct);
    Task AddAsync(Document document, CancellationToken ct);
    void Update(Document document);
}
