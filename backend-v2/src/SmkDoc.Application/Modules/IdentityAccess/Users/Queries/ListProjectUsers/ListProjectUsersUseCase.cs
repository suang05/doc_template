using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersUseCase(
    IUserProjectRoleRepository roleRepo,
    IUserRepository userRepo) : IUseCase<ListProjectUsersQuery, List<UserResultDto>>
{
    public async Task<List<UserResultDto>> ExecuteAsync(ListProjectUsersQuery query, CancellationToken ct)
    {
        var roles = await roleRepo.ListForCurrentProjectAsync(ct);
        var userIds = roles.Select(r => r.UserId).ToHashSet();
        var users = await userRepo.GetByIdsAsync(userIds, ct);

        var userMap = users.ToDictionary(u => u.Id);
        return roles
            .Where(r => userMap.ContainsKey(r.UserId))
            .Select(r => ToUserResultDto(r, userMap[r.UserId]))
            .OrderBy(u => u.Email)
            .ToList();
    }

    private static UserResultDto ToUserResultDto(Domain.Entities.UserProjectRole r, Domain.Entities.User u) =>
        new(u.Id.Value,
            u.Email.Value,
            u.FirstName,
            u.LastName,
            r.Role.ToString(),
            u.IsActive,
            u.CreatedAt,
            r.GetConcurrencyToken());
}
