using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface IDocumentJobRepository
{
    Task<DocumentJob?> GetByIdAsync(DocumentJobId id, CancellationToken ct);
    Task<DocumentJob?> GetByIdWithItemsAsync(DocumentJobId id, CancellationToken ct);
    Task AddAsync(DocumentJob job, CancellationToken ct);
    void Update(DocumentJob job);
}
