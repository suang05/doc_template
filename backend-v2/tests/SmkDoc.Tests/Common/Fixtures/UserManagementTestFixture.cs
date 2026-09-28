using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Common.Fixtures;

public class UserManagementTestFixture
{
    public Mock<IUserRepository> UserRepo { get; } = new();
    public Mock<IUserProjectRoleRepository> RoleRepo { get; } = new();
    public Mock<IUnitOfWork> Uow { get; } = new();
    public Mock<IPasswordHasher> PasswordHasher { get; } = new();

    public RemoveUserUseCase BuildRemoveUserUseCase() => new(
        RoleRepo.Object,
        Uow.Object,
        new RemoveUserCommandValidator()
    );

    public UpdateUserRoleUseCase BuildUpdateUserRoleUseCase() => new(
        RoleRepo.Object,
        Uow.Object,
        new UpdateUserRoleCommandValidator()
    );

    public SetUserStatusUseCase BuildSetUserStatusUseCase() => new(
        UserRepo.Object,
        RoleRepo.Object,
        Uow.Object,
        new SetUserStatusCommandValidator()
    );

    public InviteUserUseCase BuildInviteUserUseCase() => new(
        UserRepo.Object,
        RoleRepo.Object,
        PasswordHasher.Object,
        Uow.Object,
        new InviteUserCommandValidator()
    );

    public ListProjectUsersUseCase BuildListProjectUsersUseCase() => new(
        RoleRepo.Object,
        UserRepo.Object,
        new ListProjectUsersQueryValidator()
    );
}
