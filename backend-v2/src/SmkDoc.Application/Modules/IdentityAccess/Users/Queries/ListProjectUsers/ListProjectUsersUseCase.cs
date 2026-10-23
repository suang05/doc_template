using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersUseCase(
    IUserProjectRoleRepository roleRepo,
    IUserRepository userRepo,
    IValidator<ListProjectUsersQuery> validator) : IUseCase<ListProjectUsersQuery, List<UserResultDto>>
{
    public async Task<List<UserResultDto>> ExecuteAsync(ListProjectUsersQuery query, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var roles = await roleRepo.ListByProjectAsync(new ProjectId(query.ProjectId), ct);
        var userIds = roles.Select(r => r.UserId).ToHashSet();
        var users = await userRepo.GetByIdsAsync(userIds, ct);

        var userMap = users.ToDictionary(u => u.Id);
        return roles
            .Where(r => userMap.ContainsKey(r.UserId))
            .Select(r =>
            {
                var u = userMap[r.UserId];
                return new UserResultDto(
                    u.Id.Value,
                    u.Email.Value,
                    u.FirstName,
                    u.LastName,
                    r.Role.ToString(),
                    u.IsActive,
                    u.CreatedAt
                );
            })
            .OrderBy(u => u.Email)
            .ToList();
    }
}
