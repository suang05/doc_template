using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetFirstAsync(CancellationToken ct = default);
    Task<Company?> GetByIdAsync(CompanyId id, CancellationToken ct = default);
    Task<IReadOnlyList<Company>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(CompanyName name, CancellationToken ct = default);
    Task AddAsync(Company company, CancellationToken ct = default);
}
