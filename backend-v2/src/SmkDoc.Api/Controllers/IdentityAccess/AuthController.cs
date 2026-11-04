using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Auth;
using SmkDoc.Api.Extensions;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Authentication and identity profile endpoints for Portal users.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IUseCase<LoginCommand, LoginResultDto> loginUseCase,
    IUseCase<RefreshTokenCommand, TokenResultDto> refreshTokenUseCase,
    IUseCase<GetCurrentUserProfileQuery, CurrentUserProfileResultDto> getCurrentUserProfileUseCase) : ControllerBase
{
    /// <summary>
    /// Authenticate a user and return a JWT access token alongside a refresh token.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(ApiResponse<LoginResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await loginUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<LoginResultDto>(result));
    }

    /// <summary>
    /// Rotate and refresh access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(ApiResponse<TokenResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await refreshTokenUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<TokenResultDto>(result));
    }

    /// <summary>
    /// Get the profile and accessible workspace projects for the currently authenticated user.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserProfileResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        var query = new GetCurrentUserProfileQuery(currentUserId);
        var result = await getCurrentUserProfileUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<CurrentUserProfileResultDto>(result));
    }
}
