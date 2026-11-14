using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Services;

/// <summary>
/// Domain application processor implementing single-source-of-truth user lookup and safe registration.
/// </summary>
public sealed class UserAccountProcessor(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher) : IUserAccountProcessor
{
    public async Task<User> GetOrRegisterUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await userRepo.GetByEmailAsync(EmailAddress.Create(normalizedEmail), ct);
        if (existingUser != null)
        {
            return existingUser;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ValidationException("Password", "Password must be at least 8 characters.");
        }

        var hash = passwordHasher.HashPassword(password);
        var newUser = User.Register(EmailAddress.Create(normalizedEmail), hash, firstName, lastName, now);
        await userRepo.AddAsync(newUser, ct);
        return newUser;
    }
}
