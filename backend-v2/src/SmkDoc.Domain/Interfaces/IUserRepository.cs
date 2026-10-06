using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    void Update(User user);
}
