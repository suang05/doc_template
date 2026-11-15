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
            .Select(r => UserResultDto.FromEntity(userMap[r.UserId], r.Role.Name, r.Version))
            .OrderBy(u => u.Email)
            .ToList();
    }
}
