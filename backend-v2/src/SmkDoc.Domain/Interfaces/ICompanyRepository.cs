using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetFirstAsync(CancellationToken ct);
    Task<Company?> GetByIdAsync(CompanyId id, CancellationToken ct);
    Task<Company?> GetByNameAsync(CompanyName name, CancellationToken ct);
    Task<IReadOnlyList<Company>> ListAsync(CancellationToken ct);
    Task AddAsync(Company company, CancellationToken ct);
}
