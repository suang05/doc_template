using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Extensions;
using SmkDoc.Api.Models;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.InviteUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.RemoveUser;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.UpdateUserRole;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.ListProjectUsers;

namespace SmkDoc.Api.Controllers;

/// <summary>
/// User management within a project scope.
/// Auth: JWT Bearer (JwtPolicy) — Admin role required for mutating operations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/users")]
[Authorize(Policy = "JwtPolicy")]
public class UserManagementController : ControllerBase
{
    /// <summary>List all users in a project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromRoute] Guid projectId,
        [FromServices] ListProjectUsersUseCase useCase,
        CancellationToken ct)
    {
        var users = await useCase.ExecuteAsync(new ListProjectUsersQuery(projectId), ct);
        var result = users.Select(u => new UserListItem(u.Id, u.Email, u.FirstName, u.LastName, u.Role, u.IsActive, u.CreatedAt));
        return Ok(new ApiResponse<IEnumerable<UserListItem>>(result));
    }

    /// <summary>Invite a new user to the project. Admin only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUser(
        [FromRoute] Guid projectId,
        [FromBody] InviteUserRequest req,
        [FromServices] InviteUserUseCase useCase,
        CancellationToken ct)
    {
        User.RequireAdmin();
        var command = new InviteUserCommand(projectId, req.Email, req.Password, req.FirstName, req.LastName, req.Role);
        var user = await useCase.ExecuteAsync(command, ct);
        var result = new UserListItem(user.Id, user.Email, user.FirstName, user.LastName, user.Role, user.IsActive, user.CreatedAt);
        return Ok(new ApiResponse<UserListItem>(result));
    }

    /// <summary>Update a user's role within the project. Admin only.</summary>
    [HttpPut("{userId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        [FromBody] UpdateUserRoleRequest req,
        [FromServices] UpdateUserRoleUseCase useCase,
        CancellationToken ct)
    {
        User.RequireAdmin();
        var command = new UpdateUserRoleCommand(projectId, userId, req.Role);
        await useCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    /// <summary>Remove a user from the project. Admin only.</summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        [FromServices] RemoveUserUseCase useCase,
        CancellationToken ct)
    {
        User.RequireAdmin();
        var currentUserId = User.GetUserId();
        var command = new RemoveUserCommand(projectId, userId, currentUserId);
        await useCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    /// <summary>Activate or deactivate a user account. Admin only.</summary>
    [HttpPatch("{userId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        [FromRoute] Guid projectId,
        [FromRoute] Guid userId,
        [FromBody] bool isActive,
        [FromServices] SetUserStatusUseCase useCase,
        CancellationToken ct)
    {
        User.RequireAdmin();
        var command = new SetUserStatusCommand(projectId, userId, isActive);
        await useCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}
