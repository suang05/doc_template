namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Coordinates atomic persistence transactions and post-commit domain event dispatch.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending mutations and dispatches queued domain events.
    /// </summary>
    Task<int> CommitAsync(CancellationToken ct);
}
