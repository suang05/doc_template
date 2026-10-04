using SmkDoc.Domain.Entities;

namespace SmkDoc.Domain.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetFirstAsync(CancellationToken ct = default);
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Company>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Company company, CancellationToken ct = default);
}
