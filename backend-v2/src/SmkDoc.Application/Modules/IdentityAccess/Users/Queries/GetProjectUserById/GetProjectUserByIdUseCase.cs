using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetProjectUserById;

public sealed class GetProjectUserByIdUseCase(
    IUserProjectRoleRepository roleRepo,
    IUserRepository userRepo) : IUseCase<GetProjectUserByIdQuery, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(GetProjectUserByIdQuery query, CancellationToken ct)
    {
        var userId = new UserId(query.UserId);
        var role = await roleRepo.GetForCurrentProjectAsync(userId, ct)
            ?? throw new NotFoundException("ProjectUser", query.UserId);
        var user = await userRepo.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", query.UserId);

        return new UserResultDto(
            user.Id.Value,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            role.Role.Name,
            user.IsActive,
            user.CreatedAt,
            role.Version);
    }
}
