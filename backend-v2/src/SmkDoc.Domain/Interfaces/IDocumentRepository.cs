using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Domain repository interface for managing <see cref="Document"/> aggregate roots.
/// </summary>
public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken ct = default);
    Task<Document?> GetByDocumentRefAsync(DocumentReference documentRef, CancellationToken ct = default);
    Task<Document?> GetByDocumentRefWithVersionsAsync(DocumentReference documentRef, CancellationToken ct = default);
    Task<DocumentVersion?> GetVersionAsync(DocumentReference documentRef, int version, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentVersion>> GetVersionHistoryAsync(DocumentReference documentRef, CancellationToken ct = default);
    Task<int> GetNextVersionNumberAsync(DocumentId documentId, CancellationToken ct = default);
    Task AddAsync(Document document, CancellationToken ct = default);
    void Update(Document document);
}
