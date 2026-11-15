using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetUserById;

public sealed class GetUserByIdUseCase(
    IUserRepository userRepo) : IUseCase<GetUserByIdQuery, UserResultDto>
{
    public async Task<UserResultDto> ExecuteAsync(GetUserByIdQuery query, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(new UserId(query.UserId), ct)
            ?? throw new NotFoundException("User", query.UserId);

        return UserResultDto.FromEntity(user, user.SystemRole.Name, user.Version);
    }
}
