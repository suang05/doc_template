namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Coordinates atomic transactions and commits changes across aggregate repositories.
/// </summary>
public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
