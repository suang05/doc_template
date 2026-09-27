using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.DTOs.Users;
using SmkDoc.Application.UseCases.Security;
using SmkDoc.Api.Extensions;

namespace SmkDoc.Api.Controllers;

/// <summary>
/// User management within a project scope.
/// Auth: JWT Bearer (JwtPolicy) — Admin role required for mutating operations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/users")]
[Authorize(Policy = "JwtPolicy")]
public class UserManagementController(UserManagementUseCase useCase) : ControllerBase
{
    /// <summary>List all users in a project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromRoute] Guid projectId, CancellationToken ct)
    {
        var users = await useCase.ListAsync(projectId, ct);
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
        CancellationToken ct)
    {
        User.RequireAdmin();
        var user = await useCase.InviteAsync(projectId, req.Email, req.Password, req.FirstName, req.LastName, req.Role, ct);
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
        CancellationToken ct)
    {
        User.RequireAdmin();
        await useCase.UpdateRoleAsync(projectId, userId, req.Role, ct);
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
        CancellationToken ct)
    {
        User.RequireAdmin();
        var currentUserId = User.GetUserId();
        await useCase.RemoveAsync(projectId, userId, currentUserId, ct);
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
        CancellationToken ct)
    {
        User.RequireAdmin();
        await useCase.SetActiveAsync(projectId, userId, isActive, ct);
        return NoContent();
    }
}
