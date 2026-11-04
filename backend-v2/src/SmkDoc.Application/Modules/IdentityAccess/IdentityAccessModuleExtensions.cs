using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Interfaces;
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
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.EventHandlers;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;
using SmkDoc.Domain.Events;

namespace SmkDoc.Application.Modules.IdentityAccess;

public static class IdentityAccessModuleExtensions
{
    public static IServiceCollection AddIdentityAccessModule(this IServiceCollection services)
    {
        // Domain Event Handlers
        services.AddScoped<IDomainEventHandler<ApiKeyRevokedDomainEvent>, ApiKeyRevokedEventHandler>();
        services.AddScoped<IDomainEventHandler<UserDeactivatedDomainEvent>, UserDeactivatedEventHandler>();

        // Users (Action-Centric Vertical Slice)
        services.AddUseCase<ListProjectUsersQuery, List<UserResultDto>, ListProjectUsersUseCase>();
        services.AddUseCase<InviteUserCommand, UserResultDto, InviteUserUseCase>();
        services.AddUseCase<UpdateUserRoleCommand, UserResultDto, UpdateUserRoleUseCase>();
        services.AddUseCase<RemoveUserCommand, RemoveUserUseCase>();
        services.AddUseCase<SetUserStatusCommand, UserResultDto, SetUserStatusUseCase>();

        // Projects (Action-Centric Vertical Slice)
        services.AddUseCase<CreateProjectCommand, ProjectResultDto, CreateProjectUseCase>();
        services.AddUseCase<TransferProjectOwnerCommand, TransferProjectOwnerUseCase>();
        services.AddUseCase<ListProjectsQuery, ProjectPagedResultDto, ListProjectsUseCase>();
        services.AddUseCase<GetProjectByIdQuery, ProjectResultDto, GetProjectByIdUseCase>();

        // Security / Auth / ApiKey (Action-Centric Vertical Slice)
        services.AddUseCase<LoginCommand, LoginResultDto, LoginUseCase>();
        services.AddUseCase<RefreshTokenCommand, TokenResultDto, RefreshTokenUseCase>();
        services.AddUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto, GetCurrentUserProfileUseCase>();
        services.AddUseCase<CreateApiKeyCommand, CreateApiKeyResultDto, CreateApiKeyUseCase>();
        services.AddUseCase<RevokeApiKeyCommand, RevokeApiKeyUseCase>();
        services.AddUseCase<ListApiKeysQuery, List<ApiKeyDto>, ListApiKeysUseCase>();
        services.AddUseCase<ValidateApiKeyQuery, ValidatedApiKeyDto?, ValidateApiKeyUseCase>();

        return services;
    }
}
