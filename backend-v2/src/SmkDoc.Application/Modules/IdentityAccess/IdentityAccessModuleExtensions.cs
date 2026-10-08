using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
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

        // Projects (Action-Centric Vertical Slice)
        services.AddScoped<CreateProjectUseCase>();
        services.AddScoped<ListProjectsUseCase>();
        services.AddScoped<GetProjectByIdUseCase>();

        // Security / Auth / ApiKey (Action-Centric Vertical Slice)
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<GetCurrentUserProfileUseCase>();
        services.AddScoped<CreateApiKeyUseCase>();
        services.AddScoped<RevokeApiKeyUseCase>();
        services.AddScoped<ListApiKeysUseCase>();
        services.AddScoped<ValidateApiKeyUseCase>();

        return services;
    }
}
