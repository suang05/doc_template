using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.EventHandlers;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.Services;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.EventHandlers;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;
using SmkDoc.Application.Modules.IdentityAccess.Users.Services;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.IdentityAccess;

public static class IdentityAccessModuleExtensions
{
    public static IServiceCollection AddIdentityAccessModule(this IServiceCollection services)
    {
        services.AddEventHandlers()
                .AddDomainServices()
                .AddUserSlices()
                .AddProjectSlices()
                .AddSecuritySlices();

        return services;
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        services.AddScoped<IDomainEventHandler<ApiKeyRevokedDomainEvent>, ApiKeyRevokedEventHandler>();
        services.AddScoped<IDomainEventHandler<UserDeactivatedDomainEvent>, UserDeactivatedEventHandler>();
        return services;
    }

    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenSessionGenerator, TokenSessionGenerator>();
        services.AddScoped<IUserAccountProcessor, UserAccountProcessor>();
        return services;
    }

    private static IServiceCollection AddUserSlices(this IServiceCollection services)
    {
        services.AddUseCase<ListProjectUsersQuery, List<UserResultDto>, ListProjectUsersUseCase>();
        services.AddUseCase<InviteUserCommand, UserResultDto, InviteUserUseCase>();
        services.AddUseCase<UpdateUserRoleCommand, UserResultDto, UpdateUserRoleUseCase>();
        services.AddUseCase<RemoveUserCommand, RemoveUserUseCase>();
        services.AddUseCase<SetUserStatusCommand, UserResultDto, SetUserStatusUseCase>();
        return services;
    }

    private static IServiceCollection AddProjectSlices(this IServiceCollection services)
    {
        services.AddUseCase<CreateProjectCommand, ProjectResultDto, CreateProjectUseCase>();
        services.AddUseCase<TransferProjectOwnerCommand, TransferProjectOwnerUseCase>();
        services.AddUseCase<ListProjectsQuery, ProjectPagedResultDto, ListProjectsUseCase>();
        services.AddUseCase<GetProjectByIdQuery, ProjectResultDto, GetProjectByIdUseCase>();
        return services;
    }

    private static void AddSecuritySlices(this IServiceCollection services)
    {
        services.AddUseCase<LoginCommand, LoginResultDto, LoginUseCase>();
        services.AddUseCase<RefreshTokenCommand, TokenResultDto, RefreshTokenUseCase>();
        services.AddUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto, GetCurrentUserProfileUseCase>();
        services.AddUseCase<CreateApiKeyCommand, CreateApiKeyResultDto, CreateApiKeyUseCase>();
        services.AddUseCase<RevokeApiKeyCommand, RevokeApiKeyUseCase>();
        services.AddUseCase<ListApiKeysQuery, List<ApiKeyResultDto>, ListApiKeysUseCase>();
        services.AddUseCase<ValidateApiKeyQuery, ValidatedApiKeyResultDto?, ValidateApiKeyUseCase>();
    }
}
