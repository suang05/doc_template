using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Binders;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.IdentityAccess.Users;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Users.Commands.SetUserStatus;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.Queries.GetUserById;

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
    IUseCase<GetUserByIdQuery, UserResultDto> getUserByIdUseCase,
    IUseCase<SetUserStatusCommand, UserResultDto> setUserStatusUseCase) : ControllerBase
{
    /// <summary>Get a global user account by ID along with its current ETag. SuperAdmin only.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid userId, CancellationToken ct)
    {
        var query = new GetUserByIdQuery(userId);
        var result = await getUserByIdUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<UserResultDto>(result));
    }

    /// <summary>Activate or deactivate a global user account. SuperAdmin only.</summary>
    [HttpPatch("{userId:guid}/status")]
    [HttpPatch("/api/v1/users/{userId:guid}/status")]
    [DeprecatedEndpoint("/api/v1/users/", "/api/v1/management/users")]
    [ProducesResponseType(typeof(ApiResponse<UserResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> SetStatus(
        [FromRoute] Guid userId,
        [FromIfMatch] long expectedVersion,
        [FromBody] SetUserStatusRequest request,
        CancellationToken ct)
    {
        var command = new SetUserStatusCommand(userId, request.IsActive, expectedVersion);
        var result = await setUserStatusUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<UserResultDto>(result));
    }
}

