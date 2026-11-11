using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Platform user account governance endpoints for global administrative control.
/// Auth: JWT Bearer — SuperAdmin role strictly required. Control Plane route.
/// </summary>
[ApiController]
[Route("api/v1/management/users")]
[Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class UserManagementController(
    IUseCase<SetUserStatusCommand, UserResultDto> setUserStatusUseCase) : ControllerBase
{
    /// <summary>Activate or deactivate a global user account. SuperAdmin only.</summary>
    [HttpPatch("{userId:guid}/status")]
    [HttpPatch("/api/v1/users/{userId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

