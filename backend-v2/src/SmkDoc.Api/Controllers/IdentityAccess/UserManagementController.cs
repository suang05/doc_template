using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Api.Extensions;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// User management within a project scope.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/users")]
[Authorize] // บังคับ JWT สำหรับทุก endpoint
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class UserManagementController(
    ListProjectUsersUseCase listProjectUsersUseCase,
    InviteUserUseCase inviteUserUseCase,
    UpdateUserRoleUseCase updateUserRoleUseCase,
    RemoveUserUseCase removeUserUseCase,
    SetUserStatusUseCase setUserStatusUseCase) : ControllerBase
{
    /// <summary>List all users in a project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromRoute] Guid projectId, CancellationToken ct)
    {
        var query = new ListProjectUsersQuery(projectId);
        var users = await listProjectUsersUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<UserResultDto>>(users));
    }

    /// <summary>Invite a new user to the project. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUser(
        [FromRoute] Guid projectId,
        [FromBody] InviteUserRequest req,
        CancellationToken ct)
    {
        var command = new InviteUserCommand(projectId, req.Email, req.Password, req.FirstName, req.LastName, req.Role);
        var user = await inviteUserUseCase.ExecuteAsync(command, ct);

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<UserResultDto>(user));
    }

    /// <summary>Update a user's role within the project. Admin only.</summary>
    [HttpPut("{userId:guid}/role")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        [FromBody] UpdateUserRoleRequest req,
        CancellationToken ct)
    {
        var command = new UpdateUserRoleCommand(projectId, userId, req.Role);
        await updateUserRoleUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    /// <summary>Remove a user from the project. Admin only.</summary>
    [HttpDelete("{userId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        var command = new RemoveUserCommand(projectId, userId, currentUserId);
        await removeUserUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    /// <summary>Activate or deactivate a user account. Admin only.</summary>
    [HttpPatch("{userId:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        [FromBody] SetUserStatusRequest req,
        CancellationToken ct)
    {
        var command = new SetUserStatusCommand(projectId, userId, req.IsActive);
        await setUserStatusUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}