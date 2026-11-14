using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Services;

/// <summary>
/// Domain application processor responsible for finding existing user accounts or safely provisioning
/// new user credentials under password complexity and uniqueness invariants.
/// </summary>
public interface IUserAccountProcessor
{
    /// <summary>
    /// Looks up a user by email, or registers and stages a new user aggregate if one does not exist.
    /// </summary>
    Task<User> GetOrRegisterUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        DateTimeOffset now,
        CancellationToken ct = default);
}
