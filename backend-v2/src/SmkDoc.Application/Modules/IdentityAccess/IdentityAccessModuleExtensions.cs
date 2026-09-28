using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Modules.IdentityAccess.Projects;
using SmkDoc.Application.Modules.IdentityAccess.Security;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

namespace SmkDoc.Application.Modules.IdentityAccess;

public static class IdentityAccessModuleExtensions
{
    public static IServiceCollection AddIdentityAccessModule(this IServiceCollection services)
    {
        // Users (Action-Centric Vertical Slice)
        services.AddScoped<ListProjectUsersUseCase>();
        services.AddScoped<InviteUserUseCase>();
        services.AddScoped<UpdateUserRoleUseCase>();
        services.AddScoped<RemoveUserUseCase>();
        services.AddScoped<SetUserStatusUseCase>();

        // Projects
        services.AddScoped<ProjectManagementUseCase>();

        // Security / Auth / ApiKey
        services.AddScoped<ApiKeyUseCase>();
        services.AddScoped<LoginUseCase>();

        return services;
    }
}
