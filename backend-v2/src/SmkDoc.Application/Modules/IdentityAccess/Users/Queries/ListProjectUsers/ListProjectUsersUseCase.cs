using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Users;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.UseCases.Users.Queries.ListProjectUsers;

public sealed class ListProjectUsersUseCase(
    IUserProjectRoleRepository roleRepo,
    IUserRepository userRepo,
    IValidator<ListProjectUsersQuery> validator) : IUseCase<ListProjectUsersQuery, List<UserResponseDto>>
{
    private readonly IUserProjectRoleRepository _roleRepo = roleRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IValidator<ListProjectUsersQuery> _validator = validator;

    public async Task<List<UserResponseDto>> ExecuteAsync(ListProjectUsersQuery query, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            throw new SmkDoc.Domain.Exceptions.ValidationException(validationResult.ToDictionary());
        }

        var roles = await _roleRepo.ListByProjectAsync(query.ProjectId, ct);
        var userIds = roles.Select(r => r.UserId).ToHashSet();
        var users = await _userRepo.GetByIdsAsync(userIds, ct);

        var userMap = users.ToDictionary(u => u.Id);
        return roles
            .Where(r => userMap.ContainsKey(r.UserId))
            .Select(r =>
            {
                var u = userMap[r.UserId];
                return new UserResponseDto(
                    u.Id,
                    u.Email,
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
