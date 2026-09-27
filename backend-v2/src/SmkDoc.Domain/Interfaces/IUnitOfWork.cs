namespace SmkDoc.Domain.Interfaces;

// ควบคุม Transaction ให้สมบูรณ์แบบ Atomic
public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
