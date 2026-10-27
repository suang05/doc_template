using Microsoft.EntityFrameworkCore;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository(AppDbContext context) : IDocumentRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Document?> GetByIdAsync(DocumentId id, CancellationToken ct = default)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<Document?> GetByDocumentRefAsync(DocumentReference documentRef, CancellationToken ct = default)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentRef == documentRef, ct);
    }

    public async Task<Document?> GetByDocumentRefWithVersionsAsync(DocumentReference documentRef, CancellationToken ct = default)
    {
        return await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.DocumentRef == documentRef, ct);
    }

    public async Task<DocumentVersion?> GetVersionAsync(DocumentReference documentRef, int version, CancellationToken ct = default)
    {
        return await _context.DocumentVersions
            .Where(v => v.Version == version && _context.Documents.Any(d => d.Id == v.DocumentId && d.DocumentRef == documentRef))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentVersion>> GetVersionHistoryAsync(DocumentReference documentRef, CancellationToken ct = default)
    {
        return await _context.DocumentVersions
            .Where(v => _context.Documents.Any(d => d.Id == v.DocumentId && d.DocumentRef == documentRef))
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);
    }

    public async Task<int> GetNextVersionNumberAsync(DocumentId documentId, CancellationToken ct = default)
    {
        var currentMax = await _context.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .MaxAsync(v => (int?)v.Version, ct);

        return (currentMax ?? 0) + 1;
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        await _context.Documents.AddAsync(document, ct);
    }

    public void Update(Document document)
    {
        _context.Documents.Update(document);
    }
}
