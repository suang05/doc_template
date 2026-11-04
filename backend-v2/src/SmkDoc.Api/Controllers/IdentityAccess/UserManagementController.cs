using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Api.Extensions;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Tenant User management endpoints operating on ambient project context.
/// Auth: JWT Bearer — ProjectAdmin role required for tenant modifications, SuperAdmin for status toggles.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class UserManagementController(
    IUseCase<ListProjectUsersQuery, List<UserResultDto>> listProjectUsersUseCase,
    IUseCase<InviteUserCommand, UserResultDto> inviteUserUseCase,
    IUseCase<UpdateUserRoleCommand, UserResultDto> updateUserRoleUseCase,
    IUseCase<RemoveUserCommand> removeUserUseCase,
    IUseCase<SetUserStatusCommand, UserResultDto> setUserStatusUseCase) : ControllerBase
{
    /// <summary>List all users in the active project.</summary>
    [HttpGet]
    [Authorize(Policy = SmkDocPolicies.RequireProjectAdmin)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var query = new ListProjectUsersQuery();
        var result = await listProjectUsersUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<UserResultDto>>(result));
    }

    /// <summary>Invite a new user to the active project.</summary>
    [HttpPost]
    [Authorize(Policy = SmkDocPolicies.RequireProjectAdmin)]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUser(
        [FromBody] InviteUserRequest request,
        CancellationToken ct)
    {
        var command = new InviteUserCommand(request.Email, request.Password, request.FirstName, request.LastName, request.Role);
        var result = await inviteUserUseCase.ExecuteAsync(command, ct);

        return CreatedAtAction(nameof(List), new ApiResponse<UserResultDto>(result));
    }

    /// <summary>Update a user's role within the active project.</summary>
    [HttpPut("{userId:guid}/role")]
    [Authorize(Policy = SmkDocPolicies.RequireProjectAdmin)]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> UpdateRole(
        [FromRoute] Guid userId,
        [FromBody] UpdateUserRoleRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new UpdateUserRoleCommand(userId, request.Role);
        var result = await updateUserRoleUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<UserResultDto>(result));
    }

    /// <summary>Remove a user from the active project.</summary>
    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = SmkDocPolicies.RequireProjectAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Remove(
        [FromRoute] Guid userId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        var command = new RemoveUserCommand(userId, currentUserId);
        await removeUserUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    /// <summary>Activate or deactivate a global user account. SuperAdmin only.</summary>
    [HttpPatch("{userId:guid}/status")]
    [Authorize(Policy = SmkDocPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        [FromRoute] Guid userId,
        [FromBody] SetUserStatusRequest request,
        CancellationToken ct)
    {
        var command = new SetUserStatusCommand(userId, request.IsActive);
        var result = await setUserStatusUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<UserResultDto>(result));
    }
}
